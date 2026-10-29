using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// A tulip cup drawn as an irregular polygon: three pointed petals on top and a rounded base.
    /// Visual state change: every time it bounces off a wall it changes to the next colour in its palette.
    /// </summary>
    public class TulipFlower : Flower
    {
        // Corners of the tulip as fractions of its bounding box (0,0 = top left, 1,1 = bottom right).
        private static readonly PointF[] Outline =
        {
            new PointF(0.15f, 0.00f),   // left petal tip
            new PointF(0.35f, 0.35f),
            new PointF(0.50f, 0.05f),   // middle petal tip
            new PointF(0.65f, 0.35f),
            new PointF(0.85f, 0.00f),   // right petal tip
            new PointF(1.00f, 0.55f),
            new PointF(0.80f, 0.95f),
            new PointF(0.50f, 1.00f),
            new PointF(0.20f, 0.95f),
            new PointF(0.00f, 0.55f)
        };

        private static readonly Color[] Palette =
        {
            Color.Crimson,
            Color.Orange,
            Color.Gold,
            Color.HotPink,
            Color.MediumOrchid,
            Color.DeepSkyBlue
        };

        // A tulip is a little narrower than it is tall.
        private const float WidthToHeight = 0.8f;

        private static readonly Color OutlineColor = Color.FromArgb(160, Color.Black);

        // Full-bloom height in pixels.
        private readonly float _bloomSize;

        private int _paletteIndex;

        /// <param name="paletteIndex">Which palette colour to start with; any whole number is allowed.</param>
        public TulipFlower(PointF velocity, float bloomSize, int paletteIndex)
            : base(velocity, Palette[WrapPaletteIndex(paletteIndex)])
        {
            _bloomSize = bloomSize;
            _paletteIndex = WrapPaletteIndex(paletteIndex);
        }

        /// <summary>Number of colours a tulip cycles through.</summary>
        public static int PaletteSize
        {
            get { return Palette.Length; }
        }

        public override SizeF Size
        {
            get
            {
                float height = _bloomSize * GrowthScale;
                return new SizeF(height * WidthToHeight, height);
            }
        }

        public override void Draw(Graphics graphics)
        {
            PointF[] corners = GetCorners();

            using (SolidBrush brush = new SolidBrush(Color))
            using (Pen outline = new Pen(OutlineColor, 1.5f))
            {
                graphics.FillPolygon(brush, corners);
                graphics.DrawPolygon(outline, corners);
            }
        }

        /// <summary>Switches to the next colour in the palette.</summary>
        protected override void OnWallBounce()
        {
            _paletteIndex = (_paletteIndex + 1) % Palette.Length;
            Color = Palette[_paletteIndex];
        }

        /// <summary>Scales the outline to the tulip's current position and size.</summary>
        private PointF[] GetCorners()
        {
            RectangleF bounds = Bounds;
            PointF[] corners = new PointF[Outline.Length];

            for (int i = 0; i < Outline.Length; i++)
            {
                corners[i] = new PointF(bounds.X + Outline[i].X * bounds.Width,
                    bounds.Y + Outline[i].Y * bounds.Height);
            }

            return corners;
        }

        /// <summary>Turns any whole number (including negatives) into a valid palette position.</summary>
        private static int WrapPaletteIndex(int index)
        {
            return (index % Palette.Length + Palette.Length) % Palette.Length;
        }
    }
}
