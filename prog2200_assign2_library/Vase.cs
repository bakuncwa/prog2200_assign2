using System;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// The single vase every lily grows from, drawn in ASCII like the lilies: a glazed pot with a flared lip, narrow
    /// neck, round body and foot, lit from the upper left with a highlight and two gold bands. It stays still at the
    /// bottom middle of the garden; its mouth is the one point every stem starts from.
    /// </summary>
    public class Vase
    {
        private const int Columns = 27;
        private const int Rows = 21;

        // Gap between the vase and the bottom of the garden, in pixels.
        private const int BottomMargin = 6;

        // How far down the vase its mouth is, as a fraction of its height.
        private const float MouthDepth = 0.04f;

        // Half the vase's width at points down its height, both as fractions of its size: (height, half width).
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

        // Bands of gold glaze, as (top, bottom) fractions of the height.
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

        /// <summary>Width of the vase in pixels.</summary>
        public int Width
        {
            get { return _bitmap.Width; }
        }

        /// <summary>Height of the vase in pixels.</summary>
        public int Height
        {
            get { return _bitmap.Height; }
        }

        /// <summary>The middle of the vase's mouth: where every stem starts.</summary>
        public PointF Mouth
        {
            get { return new PointF(_location.X + Width / 2f, _location.Y + Height * MouthDepth); }
        }

        /// <summary>Puts the vase at the bottom middle of <paramref name="bounds"/>. Called whenever the garden is resized.</summary>
        public void Place(Rectangle bounds)
        {
            _location = new Point(bounds.Left + (bounds.Width - Width) / 2, bounds.Bottom - Height - BottomMargin);
        }

        /// <summary>Whether <paramref name="point"/> is on the vase itself (not just inside the box around it).</summary>
        public bool Contains(Point point)
        {
            float across = (point.X - _location.X) / (float)Width - 0.5f;
            float down = (point.Y - _location.Y) / (float)Height;

            return down >= 0 && down <= 1 && Math.Abs(across) <= HalfWidth(down);
        }

        public void Draw(Graphics graphics)
        {
            graphics.DrawImage(_bitmap, new Rectangle(_location, _bitmap.Size));
        }

        /// <summary>Half the vase's width (as a fraction of its width) at <paramref name="down"/> of the way down.</summary>
        private static float HalfWidth(float down)
        {
            for (int i = 1; i < Profile.Length; i++)
            {
                if (down <= Profile[i].X)
                {
                    PointF above = Profile[i - 1];
                    PointF below = Profile[i];
                    float t = (down - above.X) / (below.X - above.X);

                    // Smoothstep, so the outline curves between the points instead of having corners.
                    t = t * t * (3 - 2 * t);

                    return above.Y + (below.Y - above.Y) * t;
                }
            }

            return Profile[Profile.Length - 1].Y;
        }

        /// <summary>How bright and what colour the vase is at pixel (<paramref name="x"/>, <paramref name="y"/>).</summary>
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

            // The dark opening, seen as an ellipse just inside the lip.
            if (down < 0.06f && Math.Abs(across) < halfWidth * 0.75f)
            {
                return new AsciiSample(0.2f, InsideColor);
            }

            // Treat the vase as round: how far round the curve the point is, and which way that surface faces.
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

            // A faint diamond pattern between the two bands.
            if (down > Bands[0].Y && down < Bands[1].X && Math.Sin(around * 10 + down * 60) > 0.6)
            {
                brightness *= 0.75f;
            }

            return new AsciiSample(edge * AsciiCanvas.Clamp01(brightness), GlazeColor);
        }
    }
}
