using System.Drawing;
using System.Windows.Forms;

namespace prog2200_assign2_library
{
    /// <summary>
    /// A flower drawn entirely from text characters.
    /// Visual state change: its ASCII art is redrawn for every growth stage, from a single "." seed to a full bloom,
    /// then droops and turns brown while it wilts.
    /// </summary>
    public class AsciiSprout : Flower
    {
        // One picture per GrowthStage, in order (Seed, Sprout, Bud, Bloom, Wilt).
        private static readonly string[] StageArt =
        {
            AsciiArt.Lines(
                "."),
            AsciiArt.Lines(
                " ,",
                " |"),
            AsciiArt.Lines(
                "  o",
                " \\|",
                "  |"),
            AsciiArt.Lines(
                " .-.",
                "( @ )",
                " '-'",
                "\\ |",
                " \\|",
                "  |"),
            AsciiArt.Lines(
                "  _",
                " (@\\",
                "    \\",
                "  |/",
                "  |")
        };

        private const TextFormatFlags ArtFormat = TextFormatFlags.NoPadding;

        private static readonly Font ArtFont = new Font("Consolas", 11f, FontStyle.Bold);
        private static readonly Color WiltColor = Color.FromArgb(160, 120, 70);

        private Size _artSize;

        public AsciiSprout(PointF velocity, Color color)
            : base(velocity, color)
        {
            _artSize = MeasureArt();
        }

        public override SizeF Size
        {
            get { return _artSize; }
        }

        public override void Draw(Graphics graphics)
        {
            Color textColor = Stage == GrowthStage.Wilt ? WiltColor : Color;

            TextRenderer.DrawText(graphics, StageArt[(int)Stage], ArtFont, Point.Round(new PointF(X, Y)),
                textColor, ArtFormat);
        }

        /// <summary>The art changes size with each stage, so measure it again.</summary>
        protected override void OnStageChanged()
        {
            _artSize = MeasureArt();
        }

        private Size MeasureArt()
        {
            return TextRenderer.MeasureText(StageArt[(int)Stage], ArtFont, System.Drawing.Size.Empty, ArtFormat);
        }
    }
}
