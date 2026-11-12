using System;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// The one vase every lily grows from: a glazed ASCII pot fixed at the bottom middle of the garden.
    /// Its mouth is the single point every stem starts from.
    /// </summary>
    public class Vase
    {
        private const int Columns = 27;
        private const int Rows = 21;

        private const int BottomMargin = 6;

        private const float MouthDepth = 0.04f;

        // Outline as (height, half width) fractions of the vase's size, from the lip down to the foot.
        private static readonly PointF[] Profile =
        {
            new PointF(0.00f, 0.30f),   // lip
            new PointF(0.06f, 0.31f),
            new PointF(0.10f, 0.19f),   // neck
            new PointF(0.24f, 0.16f),
            new PointF(0.42f, 0.44f),   // shoulder
            new PointF(0.62f, 0.47f),   // widest part of the body
            new PointF(0.82f, 0.36f),
            new PointF(0.92f, 0.23f),   // foot
            new PointF(1.00f, 0.28f)
        };

        // Gold bands as (top, bottom) fractions of the height.
        private static readonly PointF[] Bands =
        {
            new PointF(0.47f, 0.51f),
            new PointF(0.70f, 0.73f)
        };

        private static readonly Color GlazeColor = Color.FromArgb(130, 180, 160);
        private static readonly Color BandColor = Color.FromArgb(205, 180, 110);
        private static readonly Color InsideColor = Color.FromArgb(60, 80, 70);

        private readonly Bitmap _bitmap;

        private Point _location;

        public Vase()
        {
            _bitmap = AsciiCanvas.Render(Columns, Rows, Sample, 31);
        }

        public int Width
        {
            get { return _bitmap.Width; }
        }

        public int Height
        {
            get { return _bitmap.Height; }
        }

        /// <summary>Where every stem starts.</summary>
        public PointF Mouth
        {
            get { return new PointF(_location.X + Width / 2f, _location.Y + Height * MouthDepth); }
        }

        /// <summary>Puts the vase at the bottom middle; called on every resize.</summary>
        public void Place(Rectangle bounds)
        {
            _location = new Point(bounds.Left + (bounds.Width - Width) / 2, bounds.Bottom - Height - BottomMargin);
        }

        public void Draw(Graphics graphics)
        {
            graphics.DrawImage(_bitmap, new Rectangle(_location, _bitmap.Size));
        }

        /// <summary>Half the vase's width (as a fraction of its width) part way down.</summary>
        private static float HalfWidth(float down)
        {
            for (int i = 1; i < Profile.Length; i++)
            {
                if (down <= Profile[i].X)
                {
                    PointF above = Profile[i - 1];
                    PointF below = Profile[i];
                    float t = (down - above.X) / (below.X - above.X);

                    t = t * t * (3 - 2 * t);

                    return above.Y + (below.Y - above.Y) * t;
                }
            }

            return Profile[Profile.Length - 1].Y;
        }

        /// <summary>Brightness and colour of the vase at a pixel, lit from the upper left.</summary>
        private AsciiSample Sample(float x, float y)
        {
            float across = x / (Columns * GlyphAtlas.CellWidth) - 0.5f;
            float down = y / (Rows * GlyphAtlas.CellHeight);
            float halfWidth = HalfWidth(down);
            float edge = AsciiCanvas.Soft(Math.Abs(across), halfWidth, 0.02f);

            if (edge <= 0)
            {
                return AsciiSample.Empty;
            }

            if (down < 0.06f && Math.Abs(across) < halfWidth * 0.75f)
            {
                return new AsciiSample(0.2f, InsideColor);
            }

            float around = Math.Max(-1f, Math.Min(across / halfWidth, 1f));
            float facing = (float)Math.Sqrt(1 - around * around);
            float light = 0.3f + 0.55f * (0.7f * facing - 0.3f * around);
            float highlight = 0.35f * (float)Math.Exp(-Math.Pow((around + 0.45f) / 0.15f, 2));
            float rim = Math.Abs(around) > 0.88f ? 0.55f : 0f;
            float brightness = Math.Max(light + highlight, rim);

            foreach (PointF band in Bands)
            {
                if (down >= band.X && down <= band.Y)
                {
                    return new AsciiSample(edge * (0.55f + 0.4f * facing), BandColor);
                }
            }

            if (down > Bands[0].Y && down < Bands[1].X && Math.Sin(around * 10 + down * 60) > 0.6)
            {
                brightness *= 0.75f;
            }

            return new AsciiSample(edge * AsciiCanvas.Clamp01(brightness), GlazeColor);
        }
    }
}
