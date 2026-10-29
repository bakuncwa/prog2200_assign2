using System;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Base class for every shape in the garden. On each timer tick a flower grows a little,
    /// drifts by its velocity and bounces off the walls of the panel it lives in.
    /// When it wilts - because its time is up or because another flower came too close - it falls under gravity,
    /// bounces off the floor and walls, and then resprouts as a new seed where it landed and drifts off again.
    /// Subclasses decide what the flower looks like (<see cref="Draw"/>) and how big it is (<see cref="Size"/>).
    /// </summary>
    public abstract class Flower
    {
        // How many timer ticks each growth stage lasts, in GrowthStage order (Seed, Sprout, Bud, Bloom, Wilt).
        private static readonly int[] StageTicks = { 50, 70, 70, 400, 110 };

        // Scale of a drawn flower at the start and end of each stage, so it grows smoothly instead of jumping.
        private static readonly float[] StageStartScale = { 0.25f, 0.40f, 0.60f, 0.85f, 1.00f };
        private static readonly float[] StageEndScale = { 0.40f, 0.60f, 0.85f, 1.00f, 0.70f };

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
        /// Called on every timer tick: grows the flower, moves it by its velocity (falling if it is wilting)
        /// and bounces it off the edges of <paramref name="bounds"/> (the panel's current client area).
        /// </summary>
        public virtual void Move(Rectangle bounds)
        {
            Grow();

            if (Stage == GrowthStage.Wilt)
            {
                Dy += FallGravity;
                Dx *= FallFriction;
            }

            X += Dx;
            Y += Dy;

            BounceOffWalls(bounds);
            OnMoved();
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

        /// <summary>Sets the flower drifting at its usual speed in a random direction.</summary>
        private void Resprout()
        {
            double angle = ResproutRandom.NextDouble() * 2 * Math.PI;

            Dx = (float)Math.Cos(angle) * _driftSpeed;
            Dy = (float)Math.Sin(angle) * _driftSpeed;
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
