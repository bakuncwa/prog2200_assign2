using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace prog2200_assign2_library
{
    /// <summary>
    /// A calla lily seen from the side, with a yellow spike (spadix) inside its trumpet.
    /// The trumpet is an irregular 13-point polygon, filled with ASCII and traced with DrawPolygon.
    /// It changes to the next palette colour every time it bounces off a wall.
    /// </summary>
    public class CallaLily : AsciiLily
    {
        // Polygon corners as fractions of the trumpet's bounding box (0,0 = top left, 1,1 = bottom right).
        private static readonly PointF[] Outline =
        {
            new PointF(0.62f, 0.00f),
            new PointF(0.85f, 0.10f),
            new PointF(1.00f, 0.28f),
            new PointF(0.82f, 0.40f),
            new PointF(0.70f, 0.62f),
            new PointF(0.60f, 0.86f),
            new PointF(0.55f, 1.00f),
            new PointF(0.45f, 1.00f),
            new PointF(0.40f, 0.82f),
            new PointF(0.28f, 0.58f),
            new PointF(0.06f, 0.38f),
            new PointF(0.00f, 0.22f),
            new PointF(0.30f, 0.16f)
        };

        private static readonly GraphicsPath OutlinePath = CreateOutlinePath();

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

        private const float SpatheSize = 0.8f;
        private const float EdgeRadius = 0.02f;
        private const int TraceAlpha = 70;

        private static readonly Color SpadixColor = Color.Gold;

        private int _paletteIndex;

        public CallaLily(PointF velocity, float bloomWidth, int paletteIndex)
            : base(velocity, Palette[WrapPaletteIndex(paletteIndex)], bloomWidth)
        {
            _paletteIndex = WrapPaletteIndex(paletteIndex);
        }

        public static int PaletteSize
        {
            get { return Palette.Length; }
        }

        /// <summary>The trumpet stands above the stem, so the stem meets the head near its bottom.</summary>
        protected override PointF StemTop
        {
            get { return new PointF(0f, HeadHeight * 0.92f); }
        }

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

            using (Pen trace = new Pen(Color.FromArgb(TraceAlpha, AsciiCanvas.Wilted(Color, WiltProgress))))
            {
                graphics.DrawPolygon(trace, corners);
            }
        }

        /// <summary>Visual state change: next palette colour on every wall bounce.</summary>
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

            // The point as a fraction of the polygon's bounding box.
            float outlineX = (x - top.X) / scale + 0.5f;
            float outlineY = 1f + (y - top.Y) / scale;

            float edgeDistance = float.MaxValue;

            for (int i = 0; i < Outline.Length; i++)
            {
                PointF start = Outline[i];
                PointF end = Outline[(i + 1) % Outline.Length];

                edgeDistance = Math.Min(edgeDistance, AsciiCanvas.DistanceToSegment(outlineX, outlineY, start, end) * scale);
            }

            // Bright edges; inside the polygon, lit from the left with faint veins.
            float intensity = Soft(edgeDistance, EdgeRadius) * 0.95f;

            if (OutlinePath.IsVisible(outlineX, outlineY))
            {
                float vein = Math.Sin(outlineX * 40 + outlineY * 8) > 0.7 ? 0.12f : 0f;

                intensity = Math.Max(intensity, 0.45f + 0.35f * (1f - outlineX) + vein);
            }

            AsciiSample spathe = new AsciiSample(intensity, Color);
            float spadix = Soft(AsciiCanvas.DistanceToSegment(outlineX, outlineY, SpadixBase, SpadixTip) * scale, 0.025f) * 0.9f;

            return AsciiSample.Brighter(spathe, new AsciiSample(spadix, SpadixColor));
        }

        /// <summary>The trumpet unfurls from smaller as the flower opens.</summary>
        private float CurrentSpatheSize
        {
            get { return SpatheSize * (0.4f + 0.6f * BloomOpen); }
        }

        /// <summary>Converts an outline point (a fraction of the polygon's box) to head units.</summary>
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

        private static int WrapPaletteIndex(int index)
        {
            return (index % Palette.Length + Palette.Length) % Palette.Length;
        }
    }
}
