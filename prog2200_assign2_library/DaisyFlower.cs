using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace prog2200_assign2_library
{
    /// <summary>
    /// A round flower head (an ellipse) with an ASCII glyph in the middle that changes as it grows.
    /// Visual state change: the head is filled with a linear gradient whose angle turns on every tick,
    /// so the colour appears to spin around the flower.
    /// </summary>
    public class DaisyFlower : Flower
    {
        // Degrees the gradient turns each tick.
        private const float GradientTurnPerTick = 6f;

        private const TextFormatFlags GlyphFormat =
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

        // Character in the middle of the head for each GrowthStage, in order.
        private static readonly string[] StageGlyphs = { ".", ",", "o", "@", "~" };

        private static readonly Font GlyphFont = new Font("Consolas", 12f, FontStyle.Bold);
        private static readonly Color PetalTipColor = Color.White;
        private static readonly Color WiltTipColor = Color.SaddleBrown;
        private static readonly Color OutlineColor = Color.FromArgb(120, Color.Black);

        // Full-bloom diameter in pixels.
        private readonly float _bloomSize;

        private float _gradientAngle;

        public DaisyFlower(PointF velocity, Color color, float bloomSize)
            : base(velocity, color)
        {
            _bloomSize = bloomSize;
        }

        public override SizeF Size
        {
            get
            {
                float diameter = _bloomSize * GrowthScale;
                return new SizeF(diameter, diameter);
            }
        }

        /// <summary>Turns the gradient, then grows and moves like every other flower.</summary>
        public override void Move(Rectangle bounds)
        {
            _gradientAngle = (_gradientAngle + GradientTurnPerTick) % 360f;
            base.Move(bounds);
        }

        public override void Draw(Graphics graphics)
        {
            RectangleF head = Bounds;
            Color tipColor = Stage == GrowthStage.Wilt ? WiltTipColor : PetalTipColor;

            using (LinearGradientBrush brush = new LinearGradientBrush(head, Color, tipColor, _gradientAngle))
            using (Pen outline = new Pen(OutlineColor))
            {
                graphics.FillEllipse(brush, head);
                graphics.DrawEllipse(outline, head);
            }

            TextRenderer.DrawText(graphics, StageGlyphs[(int)Stage], GlyphFont, Rectangle.Round(head),
                Color.Black, GlyphFormat);
        }
    }
}
