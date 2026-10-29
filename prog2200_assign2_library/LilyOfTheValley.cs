using System;
using System.Drawing;
using System.Windows.Forms;

namespace prog2200_assign2_library
{
    /// <summary>
    /// A lily of the valley - an arched stem with little bells hanging from it between two broad leaves - drawn in
    /// ASCII and shown in a Windows Forms <see cref="Label"/> control, a pre-existing library control used as one of
    /// the shapes. The label is added to the panel and moved by setting its Location; its text is the character grid.
    /// It is a double-buffered Label (<see cref="BufferedLabel"/>) so its text can change every tick without flicker.
    /// Visual state change: when it bumps into another flower its colours swap for a moment.
    /// </summary>
    public class LilyOfTheValley : AsciiLily
    {
        // How many ticks the colours stay swapped after a collision.
        private const int FlashTicks = 12;

        private const int BellCount = 4;

        // How many straight pieces the arched stem is measured with.
        private const int StemPieces = 12;

        // Where the arched stem starts, bends towards and ends, in plant units (it starts in the soil).
        private const float StemStartX = -0.18f;
        private const float ArchControlX = -0.20f;
        private const float ArchControlY = -0.05f;
        private const float ArchEndX = 0.32f;
        private const float ArchEndY = 0.55f;

        // Space between the art and the label's edge.
        private static readonly Padding ArtPadding = new Padding(2);

        private static readonly Font ArtFont = new Font("Consolas", 7f, FontStyle.Bold);
        private static readonly Size ArtCell = MeasureCell();
        private static readonly Color BellColor = Color.FromArgb(225, 240, 205);

        private readonly Label _label;
        private readonly Control _parent;

        private int _flashTicksLeft;

        /// <param name="parent">The panel the label is placed on. The label's background matches it.</param>
        public LilyOfTheValley(PointF velocity, float bloomWidth, Control parent)
            : base(velocity, BellColor, bloomWidth)
        {
            if (parent == null)
            {
                throw new ArgumentNullException("parent");
            }

            _parent = parent;
            _label = new BufferedLabel
            {
                AutoSize = false,
                Font = ArtFont,
                Padding = ArtPadding,
                TextAlign = ContentAlignment.TopLeft,
                UseMnemonic = false
            };

            Rasterize();
            ShowColors();
            _parent.Controls.Add(_label);
        }

        public override SizeF Size
        {
            get { return _label.Size; }
        }

        /// <summary>A Label puts one font line between rows, so the grid uses the label's own character size.</summary>
        protected override Size CellSize
        {
            get { return ArtCell; }
        }

        /// <summary>Grows and moves like every other lily, then counts down the collision flash.</summary>
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

        /// <summary>Wilts like every other lily, and swaps the label's colours for <see cref="FlashTicks"/> ticks.</summary>
        public override void OnCollision()
        {
            base.OnCollision();

            _flashTicksLeft = FlashTicks;
            ShowColors();
        }

        /// <summary>Takes the label off the panel and frees it.</summary>
        public override void Remove()
        {
            base.Remove();

            _parent.Controls.Remove(_label);
            _label.Dispose();
        }

        /// <summary>Shows the new character grid in the label and sizes the label to fit it.</summary>
        protected override void OnRasterized()
        {
            string art = GridText();

            if (art == _label.Text)
            {
                return;
            }

            Size artSize = TextRenderer.MeasureText(art, ArtFont);

            _label.Text = art;
            _label.Size = new Size(artSize.Width + ArtPadding.Horizontal, artSize.Height + ArtPadding.Vertical);
        }

        /// <summary>Keeps the label on top of the flower's position.</summary>
        protected override void OnMoved()
        {
            _label.Location = Point.Round(new PointF(X, Y));
        }

        /// <summary>An arched stem that grows along its curve, between two broad leaves rising from the soil.</summary>
        protected override AsciiSample SampleStem(float x, float y)
        {
            float growth = StemGrowth;

            if (growth <= 0)
            {
                return AsciiSample.Empty;
            }

            float distance = float.MaxValue;
            PointF previous = PointOnArch(0f);

            for (int i = 1; i <= StemPieces; i++)
            {
                PointF next = PointOnArch(growth * i / StemPieces);
                distance = Math.Min(distance, DistanceToSegment(x, y, previous, next));
                previous = next;
            }

            AsciiSample result = new AsciiSample(Soft(distance, 0.02f) * 0.7f, StemColor);

            float leafGrowth = Clamp01((growth - 0.2f) / 0.5f);

            if (leafGrowth > 0)
            {
                PointF leafBase = new PointF(StemStartX + 0.03f, SoilY);
                PointF leftTip = new PointF(leafBase.X - 0.22f * leafGrowth, SoilY - 0.75f * leafGrowth);
                PointF rightTip = new PointF(leafBase.X + 0.4f * leafGrowth, SoilY - 0.55f * leafGrowth);
                float leaf = Math.Max(LeafBlade(x, y, leafBase, leftTip, 0.09f * leafGrowth, -0.1f),
                    LeafBlade(x, y, leafBase, rightTip, 0.08f * leafGrowth, 0.15f));

                result = AsciiSample.Brighter(result, new AsciiSample(leaf, LeafColor));
            }

            return result;
        }

        /// <summary>The bells are this lily's buds, so there is no separate bud on the end of the stem.</summary>
        protected override AsciiSample SampleBud(float x, float y)
        {
            return AsciiSample.Empty;
        }

        /// <summary>
        /// Bells hanging from the outer part of the arch. They start as small green buds and swell into white bells,
        /// the ones nearest the stem first.
        /// </summary>
        protected override AsciiSample SampleBloom(float x, float y)
        {
            float growth = BudSize * 0.5f + BloomOpen * 0.5f;

            if (growth <= 0)
            {
                return AsciiSample.Empty;
            }

            Color bellColor = Blend(LeafColor, Color, BloomOpen);
            AsciiSample result = AsciiSample.Empty;

            for (int i = 0; i < BellCount; i++)
            {
                float position = 0.62f + 0.12f * i;
                float bellGrowth = Clamp01(growth * 1.6f - i * 0.2f);

                if (position > StemGrowth || bellGrowth <= 0)
                {
                    continue;
                }

                PointF hook = PointOnArch(position);
                float bellRadius = 0.03f + 0.04f * bellGrowth;
                PointF bellTop = new PointF(hook.X, hook.Y + 0.1f);
                PointF bellCentre = new PointF(hook.X, bellTop.Y + bellRadius);

                // A short stalk, the round bell, and a bright flared rim along its bottom.
                float stalk = Segment(x, y, hook, bellTop, 0.012f) * 0.6f;
                float bell = Disc(x, y, bellCentre, bellRadius) * (0.55f + 0.4f * bellGrowth);
                float rim = Segment(x, y,
                    new PointF(bellCentre.X - bellRadius * 1.1f, bellCentre.Y + bellRadius * 0.8f),
                    new PointF(bellCentre.X + bellRadius * 1.1f, bellCentre.Y + bellRadius * 0.8f), 0.012f) * 0.9f;

                result = AsciiSample.Brighter(result, new AsciiSample(stalk, StemColor));
                result = AsciiSample.Brighter(result, new AsciiSample(Math.Max(bell, rim), bellColor));
            }

            return result;
        }

        /// <summary>
        /// A point on the arched stem: 0 is where it leaves the soil and 1 is its tip. The arch is a curve that
        /// bends from the soil up and over to the right; it sways with the stem top and sags as the flower wilts.
        /// </summary>
        private PointF PointOnArch(float along)
        {
            float sway = StemTop.X;
            float sag = Wilting * 0.15f;

            PointF start = new PointF(StemStartX, SoilY);
            PointF control = new PointF(ArchControlX + sway, ArchControlY);
            PointF end = new PointF(ArchEndX + sway, ArchEndY + sag);

            float before = 1f - along;

            return new PointF(
                before * before * start.X + 2 * before * along * control.X + along * along * end.X,
                before * before * start.Y + 2 * before * along * control.Y + along * along * end.Y);
        }

        /// <summary>Normally pale text on the panel's own background; flashing swaps the two.</summary>
        private void ShowColors()
        {
            bool isFlashing = _flashTicksLeft > 0;

            _label.ForeColor = isFlashing ? _parent.BackColor : Color;
            _label.BackColor = isFlashing ? Color : _parent.BackColor;
        }

        /// <summary>Width of one character and height of one line of <see cref="ArtFont"/> as a Label draws it.</summary>
        private static Size MeasureCell()
        {
            const string sample = "MMMMMMMMMM";
            Size size = TextRenderer.MeasureText(sample, ArtFont, System.Drawing.Size.Empty, TextFormatFlags.NoPadding);

            return new Size((int)Math.Round(size.Width / (float)sample.Length), size.Height);
        }
    }
}
