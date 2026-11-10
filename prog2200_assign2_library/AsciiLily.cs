using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Abstract base for a lily head drawn in ASCII, the way TouchDesigner's ASCII effect works: the head is a
    /// brightness field, sampled on a grid of character cells, and each cell shows the character from
    /// <see cref="Ramp"/> that matches its brightness. The grid is redrawn every few ticks (stop-motion) and copied
    /// into one bitmap, which is drawn with a single DrawImage call.
    /// Shapes are described in "head units": x from -0.5 to 0.5 across the head, y downwards from 0 at the top.
    /// </summary>
    public abstract class AsciiLily : Flower
    {
        private const string DataGlyphs = "-.:'|";
        private const int MinColumns = 7;
        private const int MinRows = 7;
        private const int SamplesAcross = 2;
        private const int SamplesDown = 2;
        private const float SpeckChance = 0.08f;
        private const int DataGlyphTicks = 6;
        private const int StopMotionTicks = 4;

        protected static readonly Color StemColor = Color.FromArgb(120, 170, 90);
        protected static readonly Color LeafColor = Color.FromArgb(165, 205, 120);
        private static readonly Color SeedColor = Color.FromArgb(215, 195, 140);

        private readonly float _bloomWidth;
        private readonly int _noiseSeed;

        private char[,] _characters;
        private Color[,] _colors;
        private int _ticksAlive;
        private bool _redrawNow;

        private Bitmap _bitmap;
        private int[] _pixels;
        private bool _bitmapIsCurrent;

        // Set by Rasterize: head units per column and per row, head height in head units, head width in pixels.
        private float _columnUnit;
        private float _rowUnit;
        private float _headHeight;
        private float _headWidthPixels;

        protected AsciiLily(PointF velocity, Color color, float bloomWidth)
            : base(velocity, color)
        {
            _bloomWidth = bloomWidth;
            _noiseSeed = (int)(velocity.X * 7919) ^ (int)(velocity.Y * 104729);
        }

        public override SizeF Size
        {
            get
            {
                Size grid = _characters == null
                    ? GridSize(GrowthScale)
                    : new Size(_characters.GetLength(1), _characters.GetLength(0));
                Size cell = CellSize;
                return new SizeF(grid.Width * cell.Width, grid.Height * cell.Height);
            }
        }

        public override PointF StemEnd
        {
            get { return _characters == null ? Center : ToPanel(StemTop); }
        }

        /// <summary>Characters from empty to brightest.</summary>
        protected virtual string Ramp
        {
            get { return AsciiCanvas.Ramp; }
        }

        protected virtual Size CellSize
        {
            get { return new Size(GlyphAtlas.CellWidth, GlyphAtlas.CellHeight); }
        }

        /// <summary>Where the stem meets the head, in head units.</summary>
        protected virtual PointF StemTop
        {
            get { return new PointF(0f, HeadHeight * 0.5f); }
        }

        protected float HeadHeight
        {
            get { return _headHeight; }
        }

        /// <summary>How far the shoot has grown, 0 to 1.</summary>
        protected float StemGrowth
        {
            get
            {
                switch (Stage)
                {
                    case GrowthStage.Seed:
                        return 0f;
                    case GrowthStage.Sprout:
                        return 0.6f * StageProgress;
                    case GrowthStage.Bud:
                        return 0.6f + 0.4f * StageProgress;
                    default:
                        return 1f;
                }
            }
        }

        /// <summary>How big the bud is, 0 to 1.</summary>
        protected float BudSize
        {
            get
            {
                switch (Stage)
                {
                    case GrowthStage.Seed:
                    case GrowthStage.Sprout:
                        return 0f;
                    case GrowthStage.Bud:
                        return StageProgress;
                    default:
                        return 1f;
                }
            }
        }

        /// <summary>How open the flower is, 0 to 1.</summary>
        protected float BloomOpen
        {
            get
            {
                switch (Stage)
                {
                    case GrowthStage.Bloom:
                        return Math.Min(1f, StageProgress * 4);
                    case GrowthStage.Wilt:
                        return 1f - 0.3f * StageProgress;
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>
        /// Moves like every flower, then redraws the grid every few ticks. If the grid changes size, the head is
        /// shifted so its stem end stays put.
        /// </summary>
        public override void Move(Rectangle bounds)
        {
            _ticksAlive++;
            base.Move(bounds);

            if (_characters != null && !_redrawNow && _ticksAlive % StopMotionTicks != 0)
            {
                return;
            }

            _redrawNow = false;

            PointF stemEndBefore = StemEnd;
            Rasterize();
            PointF stemEndAfter = StemEnd;

            if (stemEndAfter != stemEndBefore)
            {
                Offset(stemEndBefore.X - stemEndAfter.X, stemEndBefore.Y - stemEndAfter.Y);
                KeepInside(bounds);
            }
        }

        public override void Draw(Graphics graphics)
        {
            if (_characters == null)
            {
                return;
            }

            if (!_bitmapIsCurrent)
            {
                ComposeBitmap();
            }

            int width = _characters.GetLength(1) * GlyphAtlas.CellWidth;
            int height = _characters.GetLength(0) * GlyphAtlas.CellHeight;
            Rectangle destination = new Rectangle((int)Math.Round(X), (int)Math.Round(Y), width, height);

            graphics.DrawImage(_bitmap, destination, new Rectangle(0, 0, width, height), GraphicsUnit.Pixel);
        }

        public override void Remove()
        {
            if (_bitmap != null)
            {
                _bitmap.Dispose();
                _bitmap = null;
            }
        }

        /// <summary>Samples the head on the grid and stores each cell's character and colour.</summary>
        protected void Rasterize()
        {
            Size grid = GridSize(GrowthScale);
            Size cell = CellSize;

            if (_characters == null || _characters.GetLength(0) != grid.Height || _characters.GetLength(1) != grid.Width)
            {
                _characters = new char[grid.Height, grid.Width];
                _colors = new Color[grid.Height, grid.Width];
            }

            _columnUnit = 1f / grid.Width;
            _rowUnit = _columnUnit * cell.Height / cell.Width;
            _headHeight = grid.Height * _rowUnit;
            _headWidthPixels = grid.Width * cell.Width;

            string ramp = Ramp;
            float wilt = WiltProgress;
            int dataTick = _ticksAlive / DataGlyphTicks;

            for (int row = 0; row < grid.Height; row++)
            {
                for (int column = 0; column < grid.Width; column++)
                {
                    // Measured from the middle so the texture stays put as the grid grows.
                    int fromCentre = column - grid.Width / 2;
                    int fromMiddle = row - grid.Height / 2;

                    AsciiSample sample = SampleCell(column, row);
                    float brightness = Texture(sample.Intensity, fromCentre, fromMiddle, wilt);
                    int level = (int)Math.Round(brightness * (ramp.Length - 1));
                    char glyph;

                    if (level > 0)
                    {
                        _characters[row, column] = ramp[level];
                        _colors[row, column] = AsciiCanvas.Shade(AsciiCanvas.Wilted(sample.Color, wilt), brightness);
                    }
                    else if (IsInsideAura(column, row, grid) && TryDataGlyph(fromCentre, fromMiddle, dataTick, out glyph))
                    {
                        _characters[row, column] = glyph;
                        _colors[row, column] = AsciiCanvas.Shade(AsciiCanvas.Wilted(Color, wilt), 0f);
                    }
                    else
                    {
                        _characters[row, column] = ' ';
                    }
                }
            }

            _bitmapIsCurrent = false;
            OnRasterized();
        }

        /// <summary>The character grid as text, one line per row (used by the Label lily).</summary>
        protected string GridText()
        {
            StringBuilder text = new StringBuilder();

            for (int row = 0; row < _characters.GetLength(0); row++)
            {
                if (row > 0)
                {
                    text.Append('\n');
                }

                for (int column = 0; column < _characters.GetLength(1); column++)
                {
                    text.Append(_characters[row, column]);
                }
            }

            return text.ToString();
        }

        protected virtual void OnRasterized()
        {
        }

        protected override void OnStageChanged()
        {
            _redrawNow = true;
        }

        /// <summary>Rubric (polymorphic processing): each kind of lily supplies its own flower shape.</summary>
        protected abstract AsciiSample SampleBloom(float x, float y);

        /// <summary>A shoot with two small leaves, which shrinks away as the bud grows.</summary>
        protected virtual AsciiSample SampleSprout(float x, float y)
        {
            float sprout = StemGrowth * (1f - BudSize);

            if (sprout <= 0)
            {
                return AsciiSample.Empty;
            }

            PointF top = StemTop;
            PointF tip = new PointF(top.X, top.Y - 0.2f * sprout);
            float shoot = Segment(x, y, top, tip, 0.02f) * 0.75f;
            float leftLeaf = LeafBlade(x, y, tip, new PointF(tip.X - 0.25f * sprout, tip.Y - 0.1f * sprout),
                0.06f * sprout, 0.3f);
            float rightLeaf = LeafBlade(x, y, tip, new PointF(tip.X + 0.25f * sprout, tip.Y - 0.1f * sprout),
                0.06f * sprout, 0.3f);

            return new AsciiSample(Math.Max(shoot, Math.Max(leftLeaf, rightLeaf)), LeafColor);
        }

        /// <summary>A closed bud that swells, then shrinks away as the flower opens.</summary>
        protected virtual AsciiSample SampleBud(float x, float y)
        {
            float bud = BudSize * (1f - BloomOpen);

            if (bud <= 0)
            {
                return AsciiSample.Empty;
            }

            PointF top = StemTop;
            float radiusX = 0.04f + 0.06f * bud;
            float radiusY = 0.07f + 0.11f * bud;
            float dx = (x - top.X) / radiusX;
            float dy = (y - (top.Y - radiusY * 0.8f)) / radiusY;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            float seam = Math.Abs(dx) < 0.15f ? 0.15f : 0f;

            return new AsciiSample(AsciiCanvas.Clamp01((1f - distance) * 3f) * (0.75f + seam),
                AsciiCanvas.Blend(LeafColor, Color, bud));
        }

        /// <summary>A pointed leaf from start to end, arched upwards, with a bright midrib and faint veins.</summary>
        protected float LeafBlade(float x, float y, PointF start, PointF end, float halfWidth, float arch)
        {
            float axisX = end.X - start.X;
            float axisY = end.Y - start.Y;
            float length = (float)Math.Sqrt(axisX * axisX + axisY * axisY);

            if (length <= 0)
            {
                return 0f;
            }

            float unitX = axisX / length;
            float unitY = axisY / length;
            float along = ((x - start.X) * unitX + (y - start.Y) * unitY) / length;

            if (along <= 0 || along >= 1)
            {
                return 0f;
            }

            float bow = arch * length * (float)Math.Sin(Math.PI * along);
            float across = Math.Abs(-(x - start.X) * unitY + (y + bow - start.Y) * unitX);
            float half = halfWidth * (float)Math.Pow(Math.Sin(Math.PI * along), 0.7);
            float edge = Soft(across, half);

            if (edge <= 0)
            {
                return 0f;
            }

            float midrib = Soft(across, half * 0.15f) * 0.95f;
            float vein = Math.Sin(along * length * 90 - across * 60) > 0.85 ? 0.15f : 0f;
            float blade = 0.6f + 0.25f * (1f - across / Math.Max(half, 0.0001f)) + vein;

            return edge * Math.Max(blade, midrib);
        }

        /// <summary>1 inside radius, fading to 0 over about one sample (anti-aliased edges).</summary>
        protected float Soft(float distance, float radius)
        {
            return AsciiCanvas.Soft(distance, radius, _columnUnit / SamplesAcross);
        }

        protected float Segment(float x, float y, PointF start, PointF end, float radius)
        {
            return Soft(AsciiCanvas.DistanceToSegment(x, y, start, end), radius);
        }

        protected float Disc(float x, float y, PointF centre, float radius)
        {
            float dx = x - centre.X;
            float dy = y - centre.Y;

            return Soft((float)Math.Sqrt(dx * dx + dy * dy), radius);
        }

        /// <summary>Converts a point in head units to panel pixels.</summary>
        protected PointF ToPanel(PointF point)
        {
            return new PointF(X + (point.X + 0.5f) * _headWidthPixels, Y + point.Y * _headWidthPixels);
        }

        /// <summary>Grid columns and rows at a given share of full size. Columns stay odd so the stem meets the middle.</summary>
        private Size GridSize(float scale)
        {
            Size cell = CellSize;
            int columns = Math.Max(MinColumns, (int)Math.Round(_bloomWidth * scale / cell.Width) | 1);
            int rows = Math.Max(MinRows, (int)Math.Round((float)columns * cell.Width / cell.Height));

            return new Size(columns, rows);
        }

        /// <summary>Samples several points in a cell and mixes the average with the brightest, so thin lines still show.</summary>
        private AsciiSample SampleCell(int column, int row)
        {
            float total = 0f;
            AsciiSample brightest = AsciiSample.Empty;

            for (int down = 0; down < SamplesDown; down++)
            {
                for (int across = 0; across < SamplesAcross; across++)
                {
                    float x = (column + (across + 0.5f) / SamplesAcross) * _columnUnit - 0.5f;
                    float y = (row + (down + 0.5f) / SamplesDown) * _rowUnit;
                    AsciiSample sample = SampleHead(x, y);

                    total += sample.Intensity;
                    brightest = AsciiSample.Brighter(brightest, sample);
                }
            }

            float average = total / (SamplesAcross * SamplesDown);

            return new AsciiSample((average + brightest.Intensity) / 2, brightest.Color);
        }

        private AsciiSample SampleHead(float x, float y)
        {
            AsciiSample result = SampleSeed(x, y);
            result = AsciiSample.Brighter(result, SampleSprout(x, y));
            result = AsciiSample.Brighter(result, SampleBud(x, y));
            return AsciiSample.Brighter(result, SampleBloom(x, y));
        }

        private AsciiSample SampleSeed(float x, float y)
        {
            float seedLeft = 1f - StemGrowth * 2;

            if (seedLeft <= 0)
            {
                return AsciiSample.Empty;
            }

            float swell = Stage == GrowthStage.Seed ? StageProgress : 1f;
            float seed = Disc(x, y, StemTop, 0.1f + 0.06f * swell) * 0.9f * seedLeft;

            return new AsciiSample(seed, SeedColor);
        }

        /// <summary>Adds grain and dark specks; while wilting, cells drop away and the rest fade out.</summary>
        private float Texture(float brightness, int fromCentre, int fromMiddle, float wilt)
        {
            if (brightness <= 0)
            {
                return 0f;
            }

            float grain = AsciiCanvas.Hash(fromCentre, fromMiddle, _noiseSeed);

            brightness *= 0.8f + 0.2f * grain;

            if (grain < SpeckChance && brightness > 0.35f)
            {
                brightness *= 0.45f;
            }

            if (wilt > 0 && AsciiCanvas.Hash(fromCentre, fromMiddle, _noiseSeed + 1) < wilt * 0.7f)
            {
                return 0f;
            }

            return AsciiCanvas.Clamp01(brightness * (1f - wilt));
        }

        /// <summary>Keeps the stray data characters inside a circle, so the grid's square outline never shows.</summary>
        private bool IsInsideAura(int column, int row, Size grid)
        {
            Size cell = CellSize;
            float dx = (column + 0.5f - grid.Width / 2f) * cell.Width;
            float dy = (row + 0.5f - grid.Height / 2f) * cell.Height;
            float radius = Math.Min(grid.Width * cell.Width, grid.Height * cell.Height) / 2f;

            return dx * dx + dy * dy < radius * radius;
        }

        /// <summary>Short flickering runs of data characters in empty cells, more of them while in bloom.</summary>
        private bool TryDataGlyph(int fromCentre, int fromMiddle, int tick, out char glyph)
        {
            float runChance = 0.02f + 0.05f * BloomOpen;
            bool inRun = AsciiCanvas.Hash(fromCentre >> 2, fromMiddle, tick + _noiseSeed) < runChance;

            if (!inRun || AsciiCanvas.Hash(fromCentre, fromMiddle, tick) < 0.35f)
            {
                glyph = ' ';
                return false;
            }

            glyph = DataGlyphs[(int)(AsciiCanvas.Hash(fromCentre, fromMiddle, tick + 7) * DataGlyphs.Length)];
            return true;
        }

        /// <summary>Copies the grid into the bitmap, making a full-bloom-sized bitmap the first time.</summary>
        private void ComposeBitmap()
        {
            int width = _characters.GetLength(1) * GlyphAtlas.CellWidth;
            int height = _characters.GetLength(0) * GlyphAtlas.CellHeight;

            if (_bitmap == null || _bitmap.Width < width || _bitmap.Height < height)
            {
                Size fullGrid = GridSize(1f);

                if (_bitmap != null)
                {
                    _bitmap.Dispose();
                }

                _bitmap = new Bitmap(Math.Max(width, fullGrid.Width * GlyphAtlas.CellWidth),
                    Math.Max(height, fullGrid.Height * GlyphAtlas.CellHeight), PixelFormat.Format32bppPArgb);
                _pixels = new int[_bitmap.Width * _bitmap.Height];
            }

            AsciiCanvas.Blit(_bitmap, _pixels, _characters, _colors);
            _bitmapIsCurrent = true;
        }
    }
}
