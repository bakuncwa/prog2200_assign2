using System;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Base class for every shape in the garden: a flower head on a stem that grows out of the vase.
    /// On each timer tick a flower grows a little, drifts by its velocity, bounces off the walls of the panel it lives
    /// in, and is held within reach of its stem (<see cref="Anchor"/>), which grows longer as the flower grows.
    /// When it wilts - because its time is up or because another flower came too close - it falls under gravity while
    /// its stem shrinks back into the vase, and then it resprouts from the vase as a new seed.
    /// Subclasses decide what the flower looks like (<see cref="Draw"/>) and how big it is (<see cref="Size"/>).
    /// </summary>
    public abstract class Flower
    {
        // How many timer ticks each growth stage lasts, in GrowthStage order (Seed, Sprout, Bud, Bloom, Wilt).
        private static readonly int[] StageTicks = { 50, 70, 70, 400, 110 };

        // Scale of a drawn flower at the start and end of each stage, so it grows smoothly instead of jumping.
        private static readonly float[] StageStartScale = { 0.25f, 0.40f, 0.60f, 0.85f, 1.00f };
        private static readonly float[] StageEndScale = { 0.40f, 0.60f, 0.85f, 1.00f, 0.70f };

        // How far the stem can reach, as a share of its full reach, at the start and end of each stage:
        // it pushes up out of the vase as the flower grows and shrinks back into it while the flower wilts.
        private static readonly float[] StageStartReach = { 0.00f, 0.06f, 0.35f, 0.65f, 1.00f };
        private static readonly float[] StageEndReach = { 0.06f, 0.35f, 0.65f, 1.00f, 0.00f };

        // Upward nudge per tick (pixels per tick) while the head hangs lower than this many pixels above the vase mouth,
        // so the bouquet keeps standing up.
        private const float Lift = 0.03f;
        private const float LiftHeight = 30f;

        // A growing flower never goes faster than this many times its drifting speed.
        private const float MaxSpeedFactor = 1.6f;

        // A resprouting flower heads off up to this many radians either side of straight up.
        private const double ResproutSpread = Math.PI / 3;

        // While wilting: pixels per tick added to the falling speed, how much sideways speed is kept each tick,
        // how much speed is kept when bouncing off the floor, and below what speed it comes to rest on the floor.
        private const float FallGravity = 0.3f;
        private const float FallFriction = 0.97f;
        private const float FloorBounce = 0.45f;
        private const float RestingSpeed = 1.5f;

        // Picks the direction a flower drifts off in when it resprouts.
        private static readonly Random ResproutRandom = new Random();

        // Speed the flower drifts at while it grows, in pixels per tick.
        private readonly float _driftSpeed;

        private int _ticksInStage;

        // Whether TieTo has been called, and the stem's longest possible reach in pixels.
        private bool _isTied;
        private float _maxReach;

        protected Flower(PointF velocity, Color color)
        {
            Dx = velocity.X;
            Dy = velocity.Y;
            _driftSpeed = (float)Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
            Color = color;
            Stage = GrowthStage.Seed;
        }

        /// <summary>Left edge of the flower, in panel coordinates.</summary>
        public float X { get; private set; }

        /// <summary>Top edge of the flower, in panel coordinates.</summary>
        public float Y { get; private set; }

        /// <summary>Horizontal speed in pixels per tick. Changed by wall bounces and collisions.</summary>
        public float Dx { get; internal set; }

        /// <summary>Vertical speed in pixels per tick. Changed by wall bounces and collisions.</summary>
        public float Dy { get; internal set; }

        /// <summary>The flower's main colour.</summary>
        public Color Color { get; protected set; }

        /// <summary>Where the flower is in its life cycle.</summary>
        public GrowthStage Stage { get; private set; }

        /// <summary>Where the stem starts: the vase mouth.</summary>
        public PointF Anchor { get; private set; }

        /// <summary>
        /// How much of the garden's full reach this flower's stem can use, from 0 to 1, so the bouquet has
        /// flowers at different heights.
        /// </summary>
        public float ReachShare { get; set; } = 1f;

        /// <summary>Where the stem meets the flower head, in panel coordinates.</summary>
        public virtual PointF StemEnd
        {
            get { return Center; }
        }

        /// <summary>How far the stem can reach right now, in pixels.</summary>
        public float CurrentReach
        {
            get
            {
                int stage = (int)Stage;
                float share = StageStartReach[stage] + (StageEndReach[stage] - StageStartReach[stage]) * StageProgress;
                return _maxReach * ReachShare * share;
            }
        }

        /// <summary>Width and height of the flower right now - changes as it grows.</summary>
        public abstract SizeF Size { get; }

        /// <summary>The rectangle the flower currently covers.</summary>
        public RectangleF Bounds
        {
            get { return new RectangleF(new PointF(X, Y), Size); }
        }

        /// <summary>Middle of the flower; the centre of its collision circle.</summary>
        public PointF Center
        {
            get
            {
                SizeF size = Size;
                return new PointF(X + size.Width / 2, Y + size.Height / 2);
            }
        }

        /// <summary>
        /// Radius of the circle used for collision detection: the average of half the width and half the height,
        /// so tall, narrow flowers do not collide with things they are nowhere near.
        /// </summary>
        public float Radius
        {
            get
            {
                SizeF size = Size;
                return (size.Width + size.Height) / 4;
            }
        }

        /// <summary>How far through the current stage the flower is, from 0 (just started) to 1 (about to change).</summary>
        protected float StageProgress
        {
            get { return (float)_ticksInStage / StageTicks[(int)Stage]; }
        }

        /// <summary>
        /// How big a drawn flower is compared to its full-bloom size, from 0.25 (a seed) to 1 (in full bloom).
        /// </summary>
        protected float GrowthScale
        {
            get
            {
                int stage = (int)Stage;
                return StageStartScale[stage] + (StageEndScale[stage] - StageStartScale[stage]) * StageProgress;
            }
        }

        /// <summary>
        /// Called on every timer tick: grows the flower, moves it by its velocity (falling if it is wilting),
        /// bounces it off the edges of <paramref name="bounds"/> (the panel's current client area) and keeps it
        /// within reach of its stem.
        /// </summary>
        public virtual void Move(Rectangle bounds)
        {
            Grow();

            if (Stage == GrowthStage.Wilt)
            {
                Dy += FallGravity;
                Dx *= FallFriction;
            }
            else
            {
                if (_isTied && StemEnd.Y > Anchor.Y - LiftHeight)
                {
                    Dy -= Lift;
                }

                LimitSpeed();
            }

            X += Dx;
            Y += Dy;

            BounceOffWalls(bounds);
            KeepWithinReach();
            OnMoved();
        }

        /// <summary>
        /// Ties the flower's stem to <paramref name="anchor"/> (the vase mouth), with a full reach of
        /// <paramref name="maxReach"/> pixels before <see cref="ReachShare"/> is applied.
        /// Called when the flower is planted and whenever the garden is resized.
        /// </summary>
        public void TieTo(PointF anchor, float maxReach)
        {
            Anchor = anchor;
            _maxReach = maxReach;
            _isTied = true;
        }

        /// <summary>Draws the flower onto the panel.</summary>
        public abstract void Draw(Graphics graphics);

        /// <summary>Called when this flower bounces off another flower. A bud or flower that is bumped starts to wilt.</summary>
        public virtual void OnCollision()
        {
            Wilt();
        }

        /// <summary>
        /// Starts wilting straight away. Only buds and flowers in bloom wilt; seeds and sprouts are too young and
        /// a wilting flower is already falling.
        /// </summary>
        public void Wilt()
        {
            if (Stage != GrowthStage.Bud && Stage != GrowthStage.Bloom)
            {
                return;
            }

            Stage = GrowthStage.Wilt;
            _ticksInStage = 0;
            OnStageChanged();
        }

        /// <summary>
        /// Releases anything the flower added to the window. Drawn flowers have nothing to release.
        /// </summary>
        public virtual void Remove()
        {
        }

        /// <summary>Places the flower so its middle is on <paramref name="point"/>.</summary>
        public void CenterOn(PointF point)
        {
            SizeF size = Size;
            X = point.X - size.Width / 2;
            Y = point.Y - size.Height / 2;
            OnMoved();
        }

        /// <summary>
        /// Pulls the flower back inside <paramref name="bounds"/>. Used when the panel shrinks
        /// and when a flower is planted close to an edge.
        /// </summary>
        public void KeepInside(Rectangle bounds)
        {
            SizeF size = Size;
            X = Clamp(X, bounds.Left, bounds.Right - size.Width);
            Y = Clamp(Y, bounds.Top, bounds.Bottom - size.Height);
            OnMoved();
        }

        /// <summary>Shifts the flower without changing its velocity (used to separate two overlapping flowers).</summary>
        internal void Offset(float dx, float dy)
        {
            X += dx;
            Y += dy;
            OnMoved();
        }

        /// <summary>Called after the flower hits a wall and changes direction.</summary>
        protected virtual void OnWallBounce()
        {
        }

        /// <summary>Called when the flower moves on to its next growth stage.</summary>
        protected virtual void OnStageChanged()
        {
        }

        /// <summary>Called whenever <see cref="X"/> or <see cref="Y"/> changes.</summary>
        protected virtual void OnMoved()
        {
        }

        /// <summary>
        /// Advances the life cycle by one tick, moving to the next stage when this one is over.
        /// After wilting it resprouts as a seed and drifts off in a new direction.
        /// </summary>
        private void Grow()
        {
            _ticksInStage++;

            if (_ticksInStage < StageTicks[(int)Stage])
            {
                return;
            }

            _ticksInStage = 0;

            if (Stage == GrowthStage.Wilt)
            {
                Stage = GrowthStage.Seed;
                Resprout();
            }
            else
            {
                Stage++;
            }

            OnStageChanged();
        }

        /// <summary>Sets the flower drifting at its usual speed in a random upward direction.</summary>
        private void Resprout()
        {
            double angle = -Math.PI / 2 + (ResproutRandom.NextDouble() * 2 - 1) * ResproutSpread;

            Dx = (float)Math.Cos(angle) * _driftSpeed;
            Dy = (float)Math.Sin(angle) * _driftSpeed;
        }

        /// <summary>
        /// If the stem end is further from the anchor than the stem can reach, pulls the flower back in and bounces
        /// it off that limit (a wilting flower is just held, not bounced, so it can be drawn back into the vase).
        /// </summary>
        private void KeepWithinReach()
        {
            if (!_isTied)
            {
                return;
            }

            PointF end = StemEnd;
            float dx = end.X - Anchor.X;
            float dy = end.Y - Anchor.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            float reach = CurrentReach;

            if (distance <= reach || distance <= 0)
            {
                return;
            }

            float unitX = dx / distance;
            float unitY = dy / distance;
            float excess = distance - reach;

            X -= unitX * excess;
            Y -= unitY * excess;

            float outward = Dx * unitX + Dy * unitY;

            if (outward > 0)
            {
                float rebound = Stage == GrowthStage.Wilt ? 1f : 2f;

                Dx -= rebound * outward * unitX;
                Dy -= rebound * outward * unitY;
            }
        }

        /// <summary>Slows the flower down if collisions or the lift have made it faster than it should drift.</summary>
        private void LimitSpeed()
        {
            float speed = (float)Math.Sqrt(Dx * Dx + Dy * Dy);
            float maxSpeed = _driftSpeed * MaxSpeedFactor;

            if (speed > maxSpeed && speed > 0)
            {
                Dx *= maxSpeed / speed;
                Dy *= maxSpeed / speed;
            }
        }

        /// <summary>
        /// If the flower has gone past an edge, puts it back on the edge and points its velocity away from that wall.
        /// A falling (wilting) flower loses speed when it hits the floor and comes to rest once it is slow enough.
        /// </summary>
        private void BounceOffWalls(Rectangle bounds)
        {
            SizeF size = Size;
            bool bounced = false;

            if (X < bounds.Left)
            {
                X = bounds.Left;
                Dx = Math.Abs(Dx);
                bounced = true;
            }
            else if (X + size.Width > bounds.Right)
            {
                X = Math.Max(bounds.Left, bounds.Right - size.Width);
                Dx = -Math.Abs(Dx);
                bounced = true;
            }

            if (Y < bounds.Top)
            {
                Y = bounds.Top;
                Dy = Math.Abs(Dy);
                bounced = true;
            }
            else if (Y + size.Height > bounds.Bottom)
            {
                Y = Math.Max(bounds.Top, bounds.Bottom - size.Height);

                if (Stage == GrowthStage.Wilt && Math.Abs(Dy) < RestingSpeed)
                {
                    Dy = 0;
                }
                else
                {
                    Dy = Stage == GrowthStage.Wilt ? -Math.Abs(Dy) * FloorBounce : -Math.Abs(Dy);
                    bounced = true;
                }
            }

            if (bounced)
            {
                OnWallBounce();
            }
        }

        /// <summary>
        /// Limits <paramref name="value"/> to the range min..max. If the panel is smaller than the flower
        /// (max below min) the flower is kept against the top or left edge.
        /// </summary>
        private static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(value, max));
        }
    }
}
