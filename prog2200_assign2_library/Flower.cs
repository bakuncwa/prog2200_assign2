using System;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Base class for every shape in the garden: a flower head on a stem that grows out of the vase.
    /// The head rides on the end of its stem: as the stem grows (<see cref="CurrentReach"/>) it carries the head out
    /// of the vase mouth (<see cref="Anchor"/>) towards the edges of the garden, so the flower blossoms outwards
    /// smoothly, while the head sways sideways by itself and bounces off the walls of the panel and off other heads.
    /// When it is time to wilt - its bloom is over or another flower came too close - it waits for its turn
    /// (<see cref="IsReadyToWilt"/>, see <see cref="WiltScheduler"/>), then droops over to one side while its stem
    /// draws it back into the vase neck, and resprouts from there as a new seed.
    /// Subclasses decide what the flower looks like (<see cref="Draw"/>) and how big it is (<see cref="Size"/>).
    /// </summary>
    public abstract class Flower
    {
        // How many timer ticks each growth stage lasts, in GrowthStage order (Seed, Sprout, Bud, Bloom, Wilt).
        // Slow on purpose, so the growth plays out like a time-lapse. Each flower stretches or squeezes these a little.
        private static readonly int[] StageTicks = { 120, 180, 180, 900, 240 };

        // Scale of a drawn flower at the start and end of each stage, so it grows smoothly instead of jumping.
        private static readonly float[] StageStartScale = { 0.25f, 0.40f, 0.60f, 0.85f, 1.00f };
        private static readonly float[] StageEndScale = { 0.40f, 0.60f, 0.85f, 1.00f, 0.70f };

        // How far the stem reaches, as a share of its full reach, at the start and end of each stage: it pushes up out
        // of the vase as the flower grows and reaches its full length by the time it blooms. While wilting it shrinks
        // back to nothing, easing in and out.
        private static readonly float[] StageStartReach = { 0.00f, 0.06f, 0.35f, 1.00f, 1.00f };
        private static readonly float[] StageEndReach = { 0.06f, 0.35f, 1.00f, 1.00f, 0.00f };

        // Each flower's stages are this much longer or shorter (as a share), picked again every time it resprouts,
        // so flowers planted together do not stay in step.
        private const double PaceJitter = 0.25;

        // How far through the bloom stage a flower is when it is fully open; a flower put in the vase already grown
        // starts from here.
        private const float FullyOpenBloomProgress = 0.25f;

        // How quickly the stem's length catches up with its reach each tick (a share of the gap), so it grows smoothly.
        private const float StemEase = 0.03f;

        // Sideways sway: how hard it pushes (pixels per tick, per tick), how fast it swings (radians per tick),
        // how hard a leaning head is pulled back upright, and how much sideways speed is kept each tick.
        private const float SwayPush = 0.006f;
        private const float SwayRate = 0.01f;
        private const float Uprighting = 0.01f;
        private const float SwayDamping = 0.995f;

        // Below this stem length the head turns as if the stem were this long, so short stems do not spin.
        private const float MinSwayRadius = 20f;

        // A flower never sways faster than this many times its drifting speed.
        private const float MaxSpeedFactor = 1.6f;

        // While wilting: radians per tick the head droops over to its side, and the angle above the vase mouth's
        // level it never droops below.
        private const float WiltDroop = 0.006f;
        private const float LowestDroopAngle = 0.15f;

        // A resprouting flower heads off up to this many radians either side of straight up.
        private const double ResproutSpread = Math.PI / 4;

        // Hitting a wall only counts as a bounce if the flower was moving into it at least this fast.
        private const float MinBounceSpeed = 0.05f;

        private const float StraightUp = (float)(-Math.PI / 2);

        // Only used on the user-interface thread.
        private static readonly Random FlowerRandom = new Random();

        // This flower's own stage lengths: StageTicks stretched or squeezed a little.
        private readonly int[] _stageTicks = new int[StageTicks.Length];

        // Speed the flower drifts at, in pixels per tick.
        private readonly float _driftSpeed;

        // Starting point of the sway, so flowers do not sway in step.
        private readonly float _swayPhase;

        private int _ticksInStage;
        private int _swayClock;

        // How big the flower was when it started wilting, so it shrinks from there instead of jumping to full size.
        private float _scaleAtWilt = 1f;

        // Whether TieTo has been called, the stem's longest possible reach in pixels, and which way it points
        // from the vase mouth (radians, screen coordinates, -pi/2 is straight up).
        private bool _isTied;
        private float _maxReach;
        private float _angle;

        protected Flower(PointF velocity, Color color)
        {
            Dx = velocity.X;
            Dy = velocity.Y;
            _driftSpeed = (float)Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
            _angle = _driftSpeed > 0 ? (float)Math.Atan2(velocity.Y, velocity.X) : StraightUp;
            _swayPhase = (float)(FlowerRandom.NextDouble() * 2 * Math.PI);
            Color = color;
            Stage = GrowthStage.Seed;
            PickPace();
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

        /// <summary>
        /// True once the flower should wilt - its bloom is over, or it was bumped while in bloom - until
        /// <see cref="Wilt"/> is called. A flower that has finished blooming stays in full bloom while it waits.
        /// </summary>
        public bool IsReadyToWilt { get; private set; }

        /// <summary>How far through wilting the flower is, from 0 (not wilting) to 1 (about to resprout).</summary>
        public float WiltProgress
        {
            get { return Stage == GrowthStage.Wilt ? StageProgress : 0f; }
        }

        /// <summary>Where the stem starts: the vase mouth.</summary>
        public PointF Anchor { get; private set; }

        /// <summary>Where the stem meets the flower head, in panel coordinates.</summary>
        public virtual PointF StemEnd
        {
            get { return Center; }
        }

        /// <summary>How far the stem reaches right now, in pixels.</summary>
        public float CurrentReach
        {
            get
            {
                int stage = (int)Stage;
                float progress = StageProgress;

                if (Stage == GrowthStage.Wilt)
                {
                    progress = progress * progress * (3 - 2 * progress);
                }

                return _maxReach * (StageStartReach[stage] + (StageEndReach[stage] - StageStartReach[stage]) * progress);
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
            get { return Math.Min(1f, (float)_ticksInStage / _stageTicks[(int)Stage]); }
        }

        /// <summary>
        /// How big a drawn flower is compared to its full-bloom size, from 0.25 (a seed) to 1 (in full bloom).
        /// </summary>
        protected float GrowthScale
        {
            get
            {
                int stage = (int)Stage;
                float start = Stage == GrowthStage.Wilt ? _scaleAtWilt : StageStartScale[stage];
                return start + (StageEndScale[stage] - start) * StageProgress;
            }
        }

        /// <summary>
        /// Called on every timer tick: grows the flower, moves it along with its stem (or by its velocity if it has
        /// no stem yet) and bounces it off the edges of <paramref name="bounds"/> (the panel's current client area).
        /// </summary>
        public virtual void Move(Rectangle bounds)
        {
            Grow();

            if (_isTied)
            {
                FollowStem();
            }
            else
            {
                X += Dx;
                Y += Dy;
            }

            BounceOffWalls(bounds);
            OnMoved();
        }

        /// <summary>
        /// Ties the flower's stem to <paramref name="anchor"/> (the vase mouth), with a full reach of
        /// <paramref name="maxReach"/> pixels.
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

        /// <summary>
        /// Called when this flower bumps into another flower. A flower in bloom that is bumped is ready to wilt;
        /// a bud just bounces, so it still gets to blossom.
        /// </summary>
        public virtual void OnCollision()
        {
            if (Stage == GrowthStage.Bloom)
            {
                IsReadyToWilt = true;
            }
        }

        /// <summary>
        /// Jumps straight to full bloom, fully open, as if the flower had been put in the vase already grown.
        /// It carries on from there like any other flower: the rest of its bloom, then wilting, then resprouting
        /// from a seed.
        /// </summary>
        public void GrowFully()
        {
            Stage = GrowthStage.Bloom;
            _ticksInStage = (int)(_stageTicks[(int)GrowthStage.Bloom] * FullyOpenBloomProgress);
            OnStageChanged();
        }

        /// <summary>
        /// Starts wilting now. Only buds and flowers in bloom wilt; seeds and sprouts are too young and a wilting
        /// flower is already wilting. Called when the <see cref="WiltScheduler"/> says it is this flower's turn.
        /// </summary>
        public void Wilt()
        {
            if (Stage != GrowthStage.Bud && Stage != GrowthStage.Bloom)
            {
                return;
            }

            _scaleAtWilt = GrowthScale;
            Stage = GrowthStage.Wilt;
            _ticksInStage = 0;
            IsReadyToWilt = false;
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

        /// <summary>The difference between two angles in radians, wrapped to the range -pi..pi.</summary>
        protected static float AngleBetween(float angle, float other)
        {
            double difference = (angle - other) % (2 * Math.PI);

            if (difference > Math.PI)
            {
                difference -= 2 * Math.PI;
            }
            else if (difference < -Math.PI)
            {
                difference += 2 * Math.PI;
            }

            return (float)difference;
        }

        /// <summary>
        /// Advances the life cycle by one tick, moving to the next stage when this one is over. A flower whose bloom
        /// is over stays in full bloom, ready to wilt, until it is told to. After wilting it resprouts as a seed.
        /// </summary>
        private void Grow()
        {
            int stage = (int)Stage;

            _ticksInStage++;

            if (_ticksInStage < _stageTicks[stage])
            {
                return;
            }

            if (Stage == GrowthStage.Bloom)
            {
                _ticksInStage = _stageTicks[stage];
                IsReadyToWilt = true;
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

        /// <summary>
        /// Moves the head along with its stem. Its angle and stem length come from where the head actually is
        /// (a collision or a wall may have pushed it), then it sways sideways, and the stem length eases towards
        /// the current reach, which carries the head out of the vase as it grows. While wilting the stem only ever
        /// gets shorter, following the shrinking reach exactly, so the head is back in the vase neck when it resprouts.
        /// </summary>
        private void FollowStem()
        {
            PointF end = StemEnd;
            float dx = end.X - Anchor.X;
            float dy = end.Y - Anchor.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);

            if (length > 1f)
            {
                _angle = (float)Math.Atan2(dy, dx);
            }

            float sideways = Dx * -(float)Math.Sin(_angle) + Dy * (float)Math.Cos(_angle);

            if (Stage == GrowthStage.Wilt)
            {
                // Droop over towards its own side, but never below the level of the vase mouth. It turns a little
                // each tick either way, so a head that was pushed too low rises back gently instead of jumping.
                float lowest = Math.Cos(_angle) >= 0 ? -LowestDroopAngle : (float)-Math.PI + LowestDroopAngle;
                _angle += Math.Max(-WiltDroop, Math.Min(AngleBetween(lowest, _angle), WiltDroop));
                sideways = 0;
            }
            else
            {
                _swayClock++;
                sideways += SwayPush * (float)Math.Sin(_swayClock * SwayRate + _swayPhase);
                sideways -= Uprighting * AngleBetween(_angle, StraightUp);
                sideways *= SwayDamping;

                float maxSpeed = _driftSpeed * MaxSpeedFactor;
                sideways = Math.Max(-maxSpeed, Math.Min(sideways, maxSpeed));

                _angle += sideways / Math.Max(length, MinSwayRadius);
            }

            length = Stage == GrowthStage.Wilt
                ? Math.Min(length, CurrentReach)
                : length + (CurrentReach - length) * StemEase;

            X += Anchor.X + (float)Math.Cos(_angle) * length - end.X;
            Y += Anchor.Y + (float)Math.Sin(_angle) * length - end.Y;

            // The head only moves sideways by itself; the stem carries it in and out.
            Dx = -(float)Math.Sin(_angle) * sideways;
            Dy = (float)Math.Cos(_angle) * sideways;
        }

        /// <summary>Puts the head back in the vase mouth and sets it off upwards in a new direction, at a new pace.</summary>
        private void Resprout()
        {
            PointF end = StemEnd;

            X += Anchor.X - end.X;
            Y += Anchor.Y - end.Y;
            _angle = (float)(StraightUp + (FlowerRandom.NextDouble() * 2 - 1) * ResproutSpread);
            Dx = 0;
            Dy = 0;
            PickPace();
        }

        /// <summary>Gives this flower its own stage lengths, a little longer or shorter than usual.</summary>
        private void PickPace()
        {
            for (int i = 0; i < StageTicks.Length; i++)
            {
                double pace = 1 + (FlowerRandom.NextDouble() * 2 - 1) * PaceJitter;
                _stageTicks[i] = Math.Max(1, (int)(StageTicks[i] * pace));
            }
        }

        /// <summary>
        /// If the flower has gone past an edge, puts it back on the edge and points its velocity away from that wall.
        /// It only counts as a bounce if the flower was actually moving into the wall, not just resting against it.
        /// </summary>
        private void BounceOffWalls(Rectangle bounds)
        {
            SizeF size = Size;
            bool bounced = false;

            if (X < bounds.Left)
            {
                X = bounds.Left;
                bounced |= Dx < -MinBounceSpeed;
                Dx = Math.Abs(Dx);
            }
            else if (X + size.Width > bounds.Right)
            {
                X = Math.Max(bounds.Left, bounds.Right - size.Width);
                bounced |= Dx > MinBounceSpeed;
                Dx = -Math.Abs(Dx);
            }

            if (Y < bounds.Top)
            {
                Y = bounds.Top;
                bounced |= Dy < -MinBounceSpeed;
                Dy = Math.Abs(Dy);
            }
            else if (Y + size.Height > bounds.Bottom)
            {
                Y = Math.Max(bounds.Top, bounds.Bottom - size.Height);
                bounced |= Dy > MinBounceSpeed;
                Dy = -Math.Abs(Dy);
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
