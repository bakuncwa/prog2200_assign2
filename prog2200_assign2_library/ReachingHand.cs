using System;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// A shaded ASCII hand that reaches down from the upper right holding a flower pinched between its thumb and first
    /// finger, lowers the flower into the vase, lets go and pulls back out of the garden.
    /// The hand is built from rounded shapes (palm, four spread fingers, thumb, wrist, sleeve) that are lit like
    /// cylinders - dark at their outlines so each finger stands apart - with creases at the joints, then drawn as
    /// ASCII once.
    /// </summary>
    public class ReachingHand
    {
        private enum Motion
        {
            Hidden,
            Reaching,
            Lowering,
            Retreating
        }

        private const int Columns = 34;
        private const int Rows = 24;

        // How many ticks each part of the movement takes.
        private const int ReachTicks = 45;
        private const int LowerTicks = 18;
        private const int RetreatTicks = 40;

        // Pixels above the vase mouth where the hand stops before lowering the flower, and where it lets go.
        private const float HoverHeight = 40f;
        private const float ReleaseHeight = 4f;

        // Where the hand starts and finishes, off the right of the garden and this far above the vase mouth.
        private const float OffscreenGap = 20f;
        private const float StartHeight = 150f;

        // Shape sizes are in "hand units": fractions of the picture's width.
        private const float FingerRadius = 0.04f;
        private const float ThumbRadius = 0.044f;
        private const float StemRadius = 0.012f;

        // The bottom of the held stem, as a fraction of the picture's width and height. It is what goes into the vase.
        private static readonly PointF StemTipFraction = new PointF(0.20f, 0.957f);

        private static readonly Color SkinColor = Color.FromArgb(232, 190, 160);
        private static readonly Color SleeveColor = Color.FromArgb(100, 120, 150);
        private static readonly Color HeldStemColor = Color.FromArgb(120, 170, 90);
        private static readonly Color HeldBudColor = Color.FromArgb(235, 120, 160);

        // Each finger points down from the palm and bends at two joints: knuckle, middle joint, tip.
        private static readonly PointF[][] Fingers =
        {
            new[] { new PointF(0.40f, 0.42f), new PointF(0.30f, 0.55f), new PointF(0.25f, 0.66f) },   // first
            new[] { new PointF(0.48f, 0.48f), new PointF(0.43f, 0.63f), new PointF(0.40f, 0.76f) },   // middle
            new[] { new PointF(0.56f, 0.48f), new PointF(0.55f, 0.62f), new PointF(0.54f, 0.73f) },   // ring
            new[] { new PointF(0.63f, 0.44f), new PointF(0.65f, 0.55f), new PointF(0.65f, 0.63f) }    // little
        };

        // The thumb comes round the left of the palm to meet the first finger's tip, pinching the stem.
        private static readonly PointF[] Thumb =
        {
            new PointF(0.43f, 0.28f), new PointF(0.31f, 0.40f), new PointF(0.24f, 0.58f)
        };

        private readonly Bitmap _holding;
        private readonly Bitmap _empty;

        private Motion _motion;
        private int _ticks;
        private PointF _stemTip;

        public ReachingHand()
        {
            _holding = AsciiCanvas.Render(Columns, Rows, (x, y) => Sample(x, y, true), 57);
            _empty = AsciiCanvas.Render(Columns, Rows, (x, y) => Sample(x, y, false), 57);
        }

        /// <summary>Whether the hand is on its way in, placing a flower, or on its way out.</summary>
        public bool IsBusy
        {
            get { return _motion != Motion.Hidden; }
        }

        /// <summary>Sends the hand in with a flower, unless it is already busy.</summary>
        public void Start()
        {
            if (IsBusy)
            {
                return;
            }

            _motion = Motion.Reaching;
            _ticks = 0;
        }

        /// <summary>
        /// Moves the hand one tick towards <paramref name="mouth"/> (the vase mouth) inside <paramref name="bounds"/>.
        /// </summary>
        /// <returns>True on the tick the hand lets go of the flower, so a new flower should be put in the vase.</returns>
        public bool Move(Rectangle bounds, PointF mouth)
        {
            if (!IsBusy)
            {
                return false;
            }

            _ticks++;

            PointF offscreen = new PointF(bounds.Right + _holding.Width * (1 - StemTipFraction.X) + OffscreenGap,
                mouth.Y - StartHeight);
            PointF above = new PointF(mouth.X, mouth.Y - HoverHeight);
            PointF release = new PointF(mouth.X, mouth.Y - ReleaseHeight);

            switch (_motion)
            {
                case Motion.Reaching:
                    _stemTip = Between(offscreen, above, (float)_ticks / ReachTicks);
                    return Advance(ReachTicks, Motion.Lowering);

                case Motion.Lowering:
                    _stemTip = Between(above, release, (float)_ticks / LowerTicks);

                    if (_ticks >= LowerTicks)
                    {
                        _motion = Motion.Retreating;
                        _ticks = 0;
                        return true;
                    }

                    return false;

                default:
                    _stemTip = Between(release, offscreen, (float)_ticks / RetreatTicks);
                    return Advance(RetreatTicks, Motion.Hidden);
            }
        }

        public void Draw(Graphics graphics)
        {
            if (!IsBusy)
            {
                return;
            }

            Bitmap picture = _motion == Motion.Retreating ? _empty : _holding;
            Point location = Point.Round(new PointF(_stemTip.X - picture.Width * StemTipFraction.X,
                _stemTip.Y - picture.Height * StemTipFraction.Y));

            graphics.DrawImage(picture, new Rectangle(location, picture.Size));
        }

        /// <summary>Moves on to <paramref name="next"/> once <paramref name="ticks"/> ticks have passed.</summary>
        private bool Advance(int ticks, Motion next)
        {
            if (_ticks >= ticks)
            {
                _motion = next;
                _ticks = 0;
            }

            return false;
        }

        /// <summary>A point part way from <paramref name="from"/> to <paramref name="to"/>, easing in and out.</summary>
        private static PointF Between(PointF from, PointF to, float progress)
        {
            float t = AsciiCanvas.Clamp01(progress);
            t = t * t * (3 - 2 * t);

            return new PointF(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t);
        }

        /// <summary>
        /// How bright and what colour the hand is at pixel (<paramref name="x"/>, <paramref name="y"/>).
        /// Parts nearer the viewer are checked first, so they cover the parts behind them.
        /// </summary>
        private static AsciiSample Sample(float x, float y, bool holdingFlower)
        {
            float width = Columns * GlyphAtlas.CellWidth;
            float handX = x / width;
            float handY = y / width;
            AsciiSample part;

            if (TryJointed(handX, handY, Thumb, ThumbRadius, out part))
            {
                return part;
            }

            foreach (PointF[] finger in Fingers)
            {
                if (TryJointed(handX, handY, finger, FingerRadius, out part))
                {
                    return part;
                }
            }

            if (TryPalm(handX, handY, out part))
            {
                return part;
            }

            // The wrist, then the sleeve over the forearm, coming down from the upper right.
            PointF wrist = new PointF(0.62f, 0.24f);
            PointF elbow = new PointF(1.15f, -0.15f);

            if (TryTube(handX, handY, new PointF(0.86f, 0.06f), elbow, 0.13f, SleeveColor, out part) ||
                TryTube(handX, handY, wrist, elbow, 0.11f, SkinColor, out part))
            {
                return part;
            }

            if (!holdingFlower)
            {
                return AsciiSample.Empty;
            }

            // The flower: a stem pinched between thumb and first finger, with a closed bud on top.
            float tipY = StemTipFraction.Y * Rows * GlyphAtlas.CellHeight / width;
            float stem = AsciiCanvas.Soft(AsciiCanvas.DistanceToSegment(handX, handY,
                new PointF(0.21f, 0.14f), new PointF(StemTipFraction.X, tipY)), StemRadius, 0.01f);
            float budX = (handX - 0.21f) / 0.045f;
            float budY = (handY - 0.09f) / 0.07f;
            float bud = AsciiCanvas.Clamp01((1 - (float)Math.Sqrt(budX * budX + budY * budY)) * 3) * 0.85f;

            return bud > stem
                ? new AsciiSample(bud, HeldBudColor)
                : new AsciiSample(stem * 0.75f, HeldStemColor);
        }

        /// <summary>A finger or thumb: rounded pieces between its joints, with a crease at each joint.</summary>
        private static bool TryJointed(float x, float y, PointF[] joints, float radius, out AsciiSample sample)
        {
            for (int i = joints.Length - 2; i >= 0; i--)
            {
                // Each piece is a little thinner than the one before it.
                float pieceRadius = radius * (1f - 0.12f * i);

                if (!TryTube(x, y, joints[i], joints[i + 1], pieceRadius, SkinColor, out sample))
                {
                    continue;
                }

                if (i > 0 && Distance(x, y, joints[i]) < pieceRadius * 0.35f)
                {
                    sample = new AsciiSample(sample.Intensity * 0.55f, SkinColor);
                }

                return true;
            }

            sample = AsciiSample.Empty;
            return false;
        }

        /// <summary>A rounded piece from <paramref name="start"/> to <paramref name="end"/>, lit like a cylinder.</summary>
        private static bool TryTube(float x, float y, PointF start, PointF end, float radius, Color color,
            out AsciiSample sample)
        {
            float distance = AsciiCanvas.DistanceToSegment(x, y, start, end);

            if (distance >= radius)
            {
                sample = AsciiSample.Empty;
                return false;
            }

            // Brightest along the middle and dark at the outline, a little brighter on the upper side.
            float facing = (float)Math.Sqrt(1 - (distance / radius) * (distance / radius));
            bool upperSide = (end.X - start.X) * (y - start.Y) - (end.Y - start.Y) * (x - start.X) < 0;
            float brightness = 0.15f + 0.75f * (float)Math.Pow(facing, 0.8) + (upperSide ? 0.05f : 0f);

            sample = new AsciiSample(brightness, color);
            return true;
        }

        /// <summary>The palm: a rounded oval, dark at its outline.</summary>
        private static bool TryPalm(float x, float y, out AsciiSample sample)
        {
            float dx = (x - 0.52f) / 0.16f;
            float dy = (y - 0.36f) / 0.15f;
            float distance = dx * dx + dy * dy;

            if (distance >= 1)
            {
                sample = AsciiSample.Empty;
                return false;
            }

            float facing = (float)Math.Sqrt(1 - distance);
            float brightness = 0.15f + 0.7f * (float)Math.Pow(facing, 0.8);

            sample = new AsciiSample(brightness, SkinColor);
            return true;
        }

        private static float Distance(float x, float y, PointF point)
        {
            float dx = x - point.X;
            float dy = y - point.Y;

            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
