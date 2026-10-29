using System;
using System.Drawing;
using System.Windows.Forms;

namespace prog2200_assign2_library
{
    /// <summary>
    /// An ASCII bouquet shown in a Windows Forms <see cref="Label"/> control - a pre-existing library control
    /// used as one of the shapes. The label is added to the panel and moved by setting its Location.
    /// Visual state change: when it bumps into another flower its colours swap for a moment.
    /// </summary>
    public class LabelFlower : Flower
    {
        // How many ticks the colours stay swapped after a collision.
        private const int FlashTicks = 12;

        // One picture per GrowthStage, in order (Seed, Sprout, Bud, Bloom, Wilt).
        private static readonly string[] StageArt =
        {
            AsciiArt.Lines(
                " .  .  ."),
            AsciiArt.Lines(
                " ,  ,  ,",
                " |  |  |"),
            AsciiArt.Lines(
                " o  o  o",
                "  \\ | /",
                "   \\|/"),
            AsciiArt.Lines(
                " @  @  @",
                "  \\ | /",
                "   \\|/",
                "   )|("),
            AsciiArt.Lines(
                " ,  ~  ,",
                "  \\ | /",
                "   \\|/",
                "   )|(")
        };

        // Space between the art and the label's border.
        private static readonly Padding ArtPadding = new Padding(4, 3, 4, 3);

        private static readonly Font ArtFont = new Font("Consolas", 9f, FontStyle.Bold);
        private static readonly Color CardColor = Color.FromArgb(20, 30, 20);

        private readonly Label _label;
        private readonly Control _parent;

        private int _flashTicksLeft;

        /// <param name="parent">The panel the label is placed on.</param>
        public LabelFlower(PointF velocity, Color color, Control parent)
            : base(velocity, color)
        {
            if (parent == null)
            {
                throw new ArgumentNullException("parent");
            }

            _parent = parent;
            _label = new Label
            {
                AutoSize = false,
                BorderStyle = BorderStyle.FixedSingle,
                Font = ArtFont,
                Padding = ArtPadding,
                TextAlign = ContentAlignment.TopLeft,
                UseMnemonic = false
            };

            ShowStageArt();
            ShowColors();
            _parent.Controls.Add(_label);
        }

        public override SizeF Size
        {
            get { return _label.Size; }
        }

        /// <summary>Grows and moves like every other flower, then counts down the collision flash.</summary>
        public override void Move(Rectangle bounds)
        {
            base.Move(bounds);

            if (_flashTicksLeft > 0)
            {
                _flashTicksLeft--;

                if (_flashTicksLeft == 0)
                {
                    ShowColors();
                }
            }
        }

        /// <summary>The Label paints itself, so there is nothing to draw on the panel.</summary>
        public override void Draw(Graphics graphics)
        {
        }

        /// <summary>Swaps the label's colours for <see cref="FlashTicks"/> ticks.</summary>
        public override void OnCollision()
        {
            _flashTicksLeft = FlashTicks;
            ShowColors();
        }

        /// <summary>Takes the label off the panel and frees it.</summary>
        public override void Remove()
        {
            _parent.Controls.Remove(_label);
            _label.Dispose();
        }

        protected override void OnStageChanged()
        {
            ShowStageArt();
        }

        /// <summary>Keeps the label on top of the flower's position.</summary>
        protected override void OnMoved()
        {
            _label.Location = Point.Round(new PointF(X, Y));
        }

        /// <summary>Shows the art for the current stage and sizes the label to fit it.</summary>
        private void ShowStageArt()
        {
            string art = StageArt[(int)Stage];
            Size artSize = TextRenderer.MeasureText(art, ArtFont);

            _label.Text = art;

            // The extra 2 pixels each way leave room for the single-line border.
            _label.Size = new Size(artSize.Width + ArtPadding.Horizontal + 2, artSize.Height + ArtPadding.Vertical + 2);
        }

        private void ShowColors()
        {
            bool isFlashing = _flashTicksLeft > 0;

            _label.ForeColor = isFlashing ? CardColor : Color;
            _label.BackColor = isFlashing ? Color : CardColor;
        }
    }
}
