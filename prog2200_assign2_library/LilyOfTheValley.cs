using System;
using System.Drawing;
using System.Windows.Forms;

namespace prog2200_assign2_library
{
    /// <summary>
    /// A lily of the valley: an arched stem with little bells hanging from it.
    /// It is shown in a Windows Forms Label (double-buffered, transparent background)
    /// whose text is the ASCII grid; the label is added to the panel and moved by setting its Location.
    /// Its text flashes gold when it bumps into another flower.
    /// </summary>
    public class LilyOfTheValley : AsciiLily
    {
        private const int FlashTicks = 12;
        private const int BellCount = 4;
        private const int StemPieces = 12;

        // The arched stem's start, bend and end points, in head units.
        private const float ArchStartX = -0.32f;
        private const float ArchStartHeight = 0.92f;
        private const float ArchControlX = -0.32f;
        private const float ArchControlY = 0.0f;
        private const float ArchEndX = 0.36f;
        private const float ArchEndY = 0.42f;

        private static readonly Padding ArtPadding = new Padding(2);
        private static readonly Font ArtFont = new Font("Consolas", 7f, FontStyle.Bold);
        private static readonly Size ArtCell = MeasureCell();
        private static readonly Color BellColor = Color.FromArgb(225, 240, 205);
        private static readonly Color FlashColor = Color.Gold;

        private readonly Label _label;
        private readonly Control _parent;

        private int _flashTicksLeft;

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
                BackColor = Color.Transparent,
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

        /// <summary>The grid uses the Label's own character size and line spacing.</summary>
        protected override Size CellSize
        {
            get { return ArtCell; }
        }

        protected override PointF StemTop
        {
            get { return new PointF(ArchStartX, HeadHeight * ArchStartHeight); }
        }

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

        /// <summary>The Label paints itself.</summary>
        public override void Draw(Graphics graphics)
        {
        }

        /// <summary>Visual state change: the text turns gold for a moment.</summary>
        public override void OnCollision()
        {
            base.OnCollision();

            _flashTicksLeft = FlashTicks;
            ShowColors();
        }

        public override void Remove()
        {
            base.Remove();

            _parent.Controls.Remove(_label);
            _label.Dispose();
        }

        protected override void OnRasterized()
        {
            ShowColors();

            string art = GridText();

            if (art == _label.Text)
            {
                return;
            }

            Size artSize = TextRenderer.MeasureText(art, ArtFont);

            _label.Text = art;
            _label.Size = new Size(artSize.Width + ArtPadding.Horizontal, artSize.Height + ArtPadding.Vertical);
        }

        protected override void OnMoved()
        {
            _label.Location = Point.Round(new PointF(X, Y));
        }

        /// <summary>Instead of a shoot, the arched stem grows along its curve, with a broad leaf beside it.</summary>
        protected override AsciiSample SampleSprout(float x, float y)
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
                distance = Math.Min(distance, AsciiCanvas.DistanceToSegment(x, y, previous, next));
                previous = next;
            }

            AsciiSample result = new AsciiSample(Soft(distance, 0.02f) * 0.7f, StemColor);
            float leafGrowth = AsciiCanvas.Clamp01((growth - 0.2f) / 0.5f);

            if (leafGrowth > 0)
            {
                PointF leafBase = StemTop;
                PointF leafTip = new PointF(leafBase.X + 0.45f * leafGrowth, leafBase.Y - 0.25f * leafGrowth);
                float leaf = LeafBlade(x, y, leafBase, leafTip, 0.07f * leafGrowth, 0.2f);

                result = AsciiSample.Brighter(result, new AsciiSample(leaf, LeafColor));
            }

            return result;
        }

        /// <summary>The bells are this lily's buds, so there is no separate bud.</summary>
        protected override AsciiSample SampleBud(float x, float y)
        {
            return AsciiSample.Empty;
        }

        /// <summary>Bells hanging from the arch, growing from green buds into white bells, nearest the stem first.</summary>
        protected override AsciiSample SampleBloom(float x, float y)
        {
            float growth = BudSize * 0.5f + BloomOpen * 0.5f;

            if (growth <= 0)
            {
                return AsciiSample.Empty;
            }

            Color bellColor = AsciiCanvas.Blend(LeafColor, Color, BloomOpen);
            AsciiSample result = AsciiSample.Empty;

            for (int i = 0; i < BellCount; i++)
            {
                float position = 0.62f + 0.12f * i;
                float bellGrowth = AsciiCanvas.Clamp01(growth * 1.6f - i * 0.2f);

                if (position > StemGrowth || bellGrowth <= 0)
                {
                    continue;
                }

                PointF hook = PointOnArch(position);
                float bellRadius = 0.03f + 0.04f * bellGrowth;
                PointF bellTop = new PointF(hook.X, hook.Y + 0.1f);
                PointF bellCentre = new PointF(hook.X, bellTop.Y + bellRadius);

                // Stalk, round bell, and a bright flared rim along its bottom.
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

        /// <summary>A point on the arch (a quadratic curve), 0 at the main stem and 1 at the tip. It sags while wilting.</summary>
        private PointF PointOnArch(float along)
        {
            PointF start = StemTop;
            PointF control = new PointF(ArchControlX, ArchControlY);
            PointF end = new PointF(ArchEndX, ArchEndY + WiltProgress * 0.15f);
            float before = 1f - along;

            return new PointF(
                before * before * start.X + 2 * before * along * control.X + along * along * end.X,
                before * before * start.Y + 2 * before * along * control.Y + along * along * end.Y);
        }

        private void ShowColors()
        {
            _label.ForeColor = _flashTicksLeft > 0 ? FlashColor : AsciiCanvas.Wilted(Color, WiltProgress);
        }

        /// <summary>Width of one character and height of one line of the font, as a Label draws it.</summary>
        private static Size MeasureCell()
        {
            const string sample = "MMMMMMMMMM";
            Size size = TextRenderer.MeasureText(sample, ArtFont, System.Drawing.Size.Empty, TextFormatFlags.NoPadding);

            return new Size((int)Math.Round(size.Width / (float)sample.Length), size.Height);
        }
    }
}
