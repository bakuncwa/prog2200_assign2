using System;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Abstract base class for every shape in the garden: a flower head on a stem that grows out of the vase.
    /// The form keeps flowers in an ArrayList and calls <see cref="Move"/> and <see cref="Draw"/> on every timer tick;
    /// the override that runs depends on each flower's real type (late binding).
    /// </summary>
    public abstract class Flower
    {
        // Timer ticks per GrowthStage (Seed, Sprout, Bud, Bloom, Wilt); each flower varies these by up to 25%.
        private static readonly int[] StageTicks = { 120, 180, 180, 900, 240 };
        private const double PaceJitter = 0.25;

        // Head size and stem reach (as shares of full size / full reach) at the start and end of each stage.
        private static readonly float[] StageStartScale = { 0.25f, 0.40f, 0.60f, 0.85f, 1.00f };
        private static readonly float[] StageEndScale = { 0.40f, 0.60f, 0.85f, 1.00f, 0.70f };
        private static readonly float[] StageStartReach = { 0.00f, 0.06f, 0.35f, 1.00f, 1.00f };
        private static readonly float[] StageEndReach = { 0.06f, 0.35f, 1.00f, 1.00f, 0.00f };

        // Share of the bloom stage after which a flower is fully open (where GrowFully starts it).
        private const float FullyOpenBloomProgress = 0.25f;

        // Stem growth per tick: a share of the gap to its reach, at most MaxStemSpeed pixels.
        private const float StemEase = 0.03f;
        private const float MaxStemSpeed = 2f;

        // After a wall bounce the stem springs back by WallRecoil pixels, fading by RecoilDecay each tick.
        private const float WallRecoil = 6f;
        private const float RecoilDecay = 0.88f;

        // Sideways sway of the head around the vase mouth.
        private const float SwayPush = 0.015f;
        private const float SwayRate = 0.008f;
        private const float Uprighting = 0.01f;
        private const float SwayDamping = 0.995f;
        private const float MinSwayRadius = 20f;
        private const float MaxSpeedFactor = 1.6f;

        // While wilting the head droops this many radians per tick, but stays this far above the vase mouth's level.
        private const float WiltDroop = 0.006f;
        private const float LowestDroopAngle = 0.15f;

        private const double ResproutSpread = Math.PI / 4;
        private const float MinBounceSpeed = 0.05f;
        private const float StraightUp = (float)(-Math.PI / 2);

        private static readonly Random FlowerRandom = new Random();

        private readonly int[] _stageTicks = new int[StageTicks.Length];
        private readonly float _driftSpeed;
        private readonly float _swayPhase;

        private int _ticksInStage;
        private int _swayClock;
        private float _recoil;
        private bool _wasTouchingWall;
        private float _scaleAtWilt = 1f;

        // The stem: where it starts (the vase mouth), its full reach in pixels, and its angle from the vase mouth.
        private PointF _anchor;
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

        public float X { get; private set; }
        public float Y { get; private set; }

        /// <summary>Velocity in pixels per tick, changed by wall bounces and collisions.</summary>
        public float Dx { get; internal set; }
        public float Dy { get; internal set; }

        public Color Color { get; protected set; }
        public GrowthStage Stage { get; private set; }

        /// <summary>True once the bloom is over or the flower was bumped in bloom, until <see cref="Wilt"/> is called.</summary>
        public bool IsReadyToWilt { get; private set; }

        /// <summary>0 when not wilting, rising to 1 at the end of the wilt.</summary>
        public float WiltProgress
        {
            get { return Stage == GrowthStage.Wilt ? StageProgress : 0f; }
        }

        /// <summary>Where the stem meets the head, in panel coordinates.</summary>
        public virtual PointF StemEnd
        {
            get { return Center; }
        }

        public abstract SizeF Size { get; }

        public PointF Center
        {
            get
            {
                SizeF size = Size;
                return new PointF(X + size.Width / 2, Y + size.Height / 2);
            }
        }

        /// <summary>Radius of the collision circle: the average of half the width and half the height.</summary>
        public float Radius
        {
            get
            {
                SizeF size = Size;
                return (size.Width + size.Height) / 4;
            }
        }

        /// <summary>How far through the current stage the flower is, from 0 to 1.</summary>
        protected float StageProgress
        {
            get { return Math.Min(1f, (float)_ticksInStage / _stageTicks[(int)Stage]); }
        }

        /// <summary>Head size as a share of its full-bloom size.</summary>
        protected float GrowthScale
        {
            get
            {
                int stage = (int)Stage;
                float start = Stage == GrowthStage.Wilt ? _scaleAtWilt : StageStartScale[stage];
                return start + (StageEndScale[stage] - start) * StageProgress;
            }
        }

        private float CurrentReach
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

        /// <summary>Called on every timer tick; subclasses override it.</summary>
        public virtual void Move(Rectangle bounds)
        {
            Grow();
            FollowStem();
            BounceOffWalls(bounds);
            OnMoved();
        }

        /// <summary>Each kind of lily draws itself.</summary>
        public abstract void Draw(Graphics graphics);

        /// <summary>
        /// Ties the stem to the vase mouth. Called again on every resize, so the reach
        /// follows the new garden size.
        /// </summary>
        public void TieTo(PointF anchor, float maxReach)
        {
            _anchor = anchor;
            _maxReach = maxReach;
        }

        /// <summary>A bloom that is bumped becomes ready to wilt.</summary>
        public virtual void OnCollision()
        {
            if (Stage == GrowthStage.Bloom)
            {
                IsReadyToWilt = true;
            }
        }

        /// <summary>Jumps straight to full bloom, for a lily added fully grown.</summary>
        public void GrowFully()
        {
            Stage = GrowthStage.Bloom;
            _ticksInStage = (int)(_stageTicks[(int)GrowthStage.Bloom] * FullyOpenBloomProgress);
            OnStageChanged();
        }

        /// <summary>Starts wilting a bud or bloom. Called when the <see cref="WiltScheduler"/> gives this flower its turn.</summary>
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

        /// <summary>Releases anything the flower added to the window.</summary>
        public virtual void Remove()
        {
        }

        public void CenterOn(PointF point)
        {
            SizeF size = Size;
            X = point.X - size.Width / 2;
            Y = point.Y - size.Height / 2;
            OnMoved();
        }

        /// <summary>Pulls the flower back inside a smaller panel.</summary>
        public void KeepInside(Rectangle bounds)
        {
            SizeF size = Size;
            X = Math.Max(bounds.Left, Math.Min(X, bounds.Right - size.Width));
            Y = Math.Max(bounds.Top, Math.Min(Y, bounds.Bottom - size.Height));
            OnMoved();
        }

        internal void Offset(float dx, float dy)
        {
            X += dx;
            Y += dy;
            OnMoved();
        }

        protected virtual void OnWallBounce()
        {
        }

        protected virtual void OnStageChanged()
        {
        }

        protected virtual void OnMoved()
        {
        }

        /// <summary>The difference between two angles in radians, wrapped to -pi..pi.</summary>
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

        /// <summary>Advances the life cycle. A finished bloom waits, ready to wilt; a finished wilt resprouts.</summary>
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
        /// Moves the head on the end of its stem: the stem length eases towards the current reach while the head sways
        /// sideways. A wilting head droops and its stem only shrinks, drawing it back into the vase.
        /// </summary>
        private void FollowStem()
        {
            PointF end = StemEnd;
            float dx = end.X - _anchor.X;
            float dy = end.Y - _anchor.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);

            // Start from where the head really is, in case a collision or a wall moved it.
            if (length > 1f)
            {
                _angle = (float)Math.Atan2(dy, dx);
            }

            float sideways = Dx * -(float)Math.Sin(_angle) + Dy * (float)Math.Cos(_angle);

            if (Stage == GrowthStage.Wilt)
            {
                float lowest = Math.Cos(_angle) >= 0 ? -LowestDroopAngle : (float)-Math.PI + LowestDroopAngle;
                _angle += Math.Max(-WiltDroop, Math.Min(AngleBetween(lowest, _angle), WiltDroop));
                sideways = 0;
                length = Math.Min(length, CurrentReach);
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

                float growth = Math.Max(-MaxStemSpeed, Math.Min((CurrentReach - length) * StemEase, MaxStemSpeed));
                length = Math.Max(0f, length + growth - _recoil);
            }

            _recoil *= RecoilDecay;

            X += _anchor.X + (float)Math.Cos(_angle) * length - end.X;
            Y += _anchor.Y + (float)Math.Sin(_angle) * length - end.Y;
            Dx = -(float)Math.Sin(_angle) * sideways;
            Dy = (float)Math.Cos(_angle) * sideways;
        }

        /// <summary>Puts the head back in the vase mouth and sends it off upwards in a new direction.</summary>
        private void Resprout()
        {
            PointF end = StemEnd;

            X += _anchor.X - end.X;
            Y += _anchor.Y - end.Y;
            _angle = (float)(StraightUp + (FlowerRandom.NextDouble() * 2 - 1) * ResproutSpread);
            Dx = 0;
            Dy = 0;
            PickPace();
        }

        /// <summary>Gives this flower stage lengths a little different from the others, so they stay out of step.</summary>
        private void PickPace()
        {
            for (int i = 0; i < StageTicks.Length; i++)
            {
                double pace = 1 + (FlowerRandom.NextDouble() * 2 - 1) * PaceJitter;
                _stageTicks[i] = Math.Max(1, (int)(StageTicks[i] * pace));
            }
        }

        /// <summary>
        /// Puts a flower that went past an edge back on it and reflects its velocity.
        /// Arriving at a wall counts as a bounce, and the stem springs back so the bounce is visible.
        /// </summary>
        private void BounceOffWalls(Rectangle bounds)
        {
            SizeF size = Size;
            bool bounced = false;
            bool isTouchingWall = false;

            if (X < bounds.Left)
            {
                X = bounds.Left;
                bounced |= Dx < -MinBounceSpeed;
                Dx = Math.Abs(Dx);
                isTouchingWall = true;
            }
            else if (X + size.Width > bounds.Right)
            {
                X = Math.Max(bounds.Left, bounds.Right - size.Width);
                bounced |= Dx > MinBounceSpeed;
                Dx = -Math.Abs(Dx);
                isTouchingWall = true;
            }

            if (Y < bounds.Top)
            {
                Y = bounds.Top;
                bounced |= Dy < -MinBounceSpeed;
                Dy = Math.Abs(Dy);
                isTouchingWall = true;
            }
            else if (Y + size.Height > bounds.Bottom)
            {
                Y = Math.Max(bounds.Top, bounds.Bottom - size.Height);
                bounced |= Dy > MinBounceSpeed;
                Dy = -Math.Abs(Dy);
                isTouchingWall = true;
            }

            bounced |= isTouchingWall && !_wasTouchingWall;
            _wasTouchingWall = isTouchingWall;

            if (!bounced)
            {
                return;
            }

            if (Stage != GrowthStage.Wilt)
            {
                _recoil = WallRecoil;
            }

            OnWallBounce();
        }
    }
}
