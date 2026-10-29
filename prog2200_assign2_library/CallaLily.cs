using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace prog2200_assign2_library
{
    /// <summary>
    /// A calla lily seen from the side. Its trumpet-shaped petal (the spathe) is an irregular polygon with a flared,
    /// pointed lip that narrows down to the stem. The polygon is filled with ASCII characters, its edges are the
    /// brightest characters, and a yellow spike (the spadix) stands up inside it. A faint line traces the polygon.
    /// Visual state change: every time it bounces off a wall it changes to the next colour in its palette.
    /// </summary>
    public class CallaLily : AsciiLily
    {
        // Corners of the spathe as fractions of its bounding box (0,0 = top left, 1,1 = bottom right).
        private static readonly PointF[] Outline =
        {
            new PointF(0.62f, 0.00f),   // pointed tip of the lip
            new PointF(0.85f, 0.10f),
            new PointF(1.00f, 0.28f),   // right side of the flared opening
            new PointF(0.82f, 0.40f),
            new PointF(0.70f, 0.62f),
            new PointF(0.60f, 0.86f),
            new PointF(0.55f, 1.00f),   // base, where the stem joins
            new PointF(0.45f, 1.00f),
            new PointF(0.40f, 0.82f),
            new PointF(0.28f, 0.58f),
            new PointF(0.06f, 0.38f),
            new PointF(0.00f, 0.22f),   // left side of the flared opening
            new PointF(0.30f, 0.16f)
        };

        // Used to test whether a point is inside the spathe.
        private static readonly GraphicsPath OutlinePath = CreateOutlinePath();

        // Bottom and top of the spadix, in the same fractions as the outline.
        private static readonly PointF SpadixBase = new PointF(0.50f, 0.92f);
        private static readonly PointF SpadixTip = new PointF(0.54f, 0.30f);

        private static readonly Color[] Palette =
        {
            Color.White,
            Color.Gold,
            Color.HotPink,
            Color.MediumOrchid,
            Color.Orange,
            Color.Crimson
        };

        // Width and height of the open spathe, in plant units.
        private const float SpatheSize = 0.62f;

        // How wide the bright edge of the spathe is, in plant units.
        private const float EdgeRadius = 0.02f;

        // Opacity (0-255) of the line that traces the polygon.
        private const int TraceAlpha = 70;

        private static readonly Color SpadixColor = Color.Gold;

        private int _paletteIndex;

        /// <param name="paletteIndex">Which palette colour to start with; any whole number is allowed.</param>
        public CallaLily(PointF velocity, float bloomWidth, int paletteIndex)
            : base(velocity, Palette[WrapPaletteIndex(paletteIndex)], bloomWidth)
        {
            _paletteIndex = WrapPaletteIndex(paletteIndex);
        }

        /// <summary>Number of colours a calla lily cycles through.</summary>
        public static int PaletteSize
        {
            get { return Palette.Length; }
        }

        /// <summary>The spathe stands up above the stem, so the stem stops lower down.</summary>
        protected override float StemTopAtFullGrowth
        {
            get { return 0.68f; }
        }

        /// <summary>Draws the ASCII plant, then traces the spathe polygon faintly over it.</summary>
        public override void Draw(Graphics graphics)
        {
            base.Draw(graphics);

            if (BloomOpen <= 0)
            {
                return;
            }

            PointF[] corners = new PointF[Outline.Length];

            for (int i = 0; i < Outline.Length; i++)
            {
                corners[i] = ToPanel(FromOutline(Outline[i]));
            }

            using (Pen trace = new Pen(Color.FromArgb(TraceAlpha, Color)))
            {
                graphics.DrawPolygon(trace, corners);
            }
        }

        /// <summary>Switches to the next colour in the palette.</summary>
        protected override void OnWallBounce()
        {
            _paletteIndex = (_paletteIndex + 1) % Palette.Length;
            Color = Palette[_paletteIndex];
        }

        protected override AsciiSample SampleBloom(float x, float y)
        {
            if (BloomOpen <= 0)
            {
                return AsciiSample.Empty;
            }

            float scale = CurrentSpatheSize;
            PointF top = StemTop;

            // The point as a fraction of the spathe's bounding box, to compare with the outline.
            float outlineX = (x - top.X) / scale + 0.5f;
            float outlineY = 1f + (y - top.Y) / scale;

            float edgeDistance = float.MaxValue;

            for (int i = 0; i < Outline.Length; i++)
            {
                PointF start = Outline[i];
                PointF end = Outline[(i + 1) % Outline.Length];

                edgeDistance = Math.Min(edgeDistance, DistanceToSegment(outlineX, outlineY, start, end) * scale);
            }

            // The edge is the brightest part; inside, the spathe is lit from the left.
            float intensity = Soft(edgeDistance, EdgeRadius) * 0.95f;

            if (OutlinePath.IsVisible(outlineX, outlineY))
            {
                intensity = Math.Max(intensity, 0.3f + 0.35f * (1f - outlineX));
            }

            AsciiSample spathe = new AsciiSample(intensity, Color);
            float spadix = Soft(DistanceToSegment(outlineX, outlineY, SpadixBase, SpadixTip) * scale, 0.025f) * 0.9f;

            return AsciiSample.Brighter(spathe, new AsciiSample(spadix, SpadixColor));
        }

        /// <summary>Width and height of the spathe right now; it unfurls from smaller as the flower opens.</summary>
        private float CurrentSpatheSize
        {
            get { return SpatheSize * (0.4f + 0.6f * BloomOpen); }
        }

        /// <summary>Converts a point of the outline (a fraction of the spathe's box) to plant units.</summary>
        private PointF FromOutline(PointF fraction)
        {
            float scale = CurrentSpatheSize;
            PointF top = StemTop;

            return new PointF(top.X + (fraction.X - 0.5f) * scale, top.Y + (fraction.Y - 1f) * scale);
        }

        private static GraphicsPath CreateOutlinePath()
        {
            GraphicsPath path = new GraphicsPath();
            path.AddPolygon(Outline);
            return path;
        }

        /// <summary>Turns any whole number (including negatives) into a valid palette position.</summary>
        private static int WrapPaletteIndex(int index)
        {
            return (index % Palette.Length + Palette.Length) % Palette.Length;
        }
    }
}
