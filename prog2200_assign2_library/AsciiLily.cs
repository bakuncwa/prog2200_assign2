using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Base class for a lily head drawn as ASCII art the way TouchDesigner's ASCII effect works: the head is
    /// described as a brightness field, the field is sampled on a grid of character cells, and each cell shows
    /// the character from <see cref="Ramp"/> that matches its brightness (dark " " up to bright "@"). The characters
    /// are copied out of a <see cref="GlyphAtlas"/> into one bitmap per lily, which is drawn in a single call.
    /// The head grows from a seed at the end of its stem into a sprout, a bud and an open flower, then crumbles away
    /// and browns as it wilts. Grain, dark specks and a few stray flickering "data" characters around the head give
    /// it the look of a TouchDesigner render. The stem itself is drawn by <see cref="Stems"/>.
    /// </summary>
    /// <remarks>
    /// The head is described in "head units": x runs from -0.5 (left edge) to 0.5 (right edge) and y runs
    /// down from 0 at the top, using the same scale, so one unit is the head's full width.
    /// </remarks>
    public abstract class AsciiLily : Flower
    {
        // Characters for the stray "data" bits that flicker in the empty space around a lily.
        private const string DataGlyphs = "-.:'|";

        // Head height compared to its width.
        private const float HeightToWidth = 1.0f;

        // Fewest columns and rows the grid can shrink to, so a seed still shows. Columns must be odd.
        private const int MinColumns = 7;
        private const int MinRows = 7;

        // Each cell is sampled on a small grid of points and the results blended, like downsampling an image.
        private const int SamplesAcross = 2;
        private const int SamplesDown = 2;

        // Fraction of cells that get a dark speck, like the texture in a TouchDesigner noise render.
        private const float SpeckChance = 0.08f;

        // How many ticks the stray data characters stay in one place before they jump somewhere else.
        private const int DataGlyphTicks = 6;

        // Wilting colour is shown in steps so the colours stay few.
        private const int WiltColorSteps = 4;

        /// <summary>Colours shared by every lily's sprout and bud: pale greens, like the reference render.</summary>
        protected static readonly Color StemColor = Color.FromArgb(120, 170, 90);
        protected static readonly Color LeafColor = Color.FromArgb(165, 205, 120);

        private static readonly Color SeedColor = Color.FromArgb(215, 195, 140);
        private static readonly Color WiltColor = Color.FromArgb(150, 110, 60);

        // Full-bloom width in pixels.
        private readonly float _bloomWidth;

        // Makes each lily's grain and specks different.
        private readonly int _noiseSeed;

        private char[,] _characters;
        private Color[,] _colors;
        private int _ticksAlive;

        // The characters copied into pixels, and whether that copy is up to date with the character grid.
        private Bitmap _bitmap;
        private int[] _pixels;
        private bool _bitmapIsCurrent;

        // Set each time the head is rasterized: head units covered by one column and one row, the head's height
        // in head units and its width in pixels.
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

        /// <summary>The size of the character grid, which grows with the flower.</summary>
        public override SizeF Size
        {
            get
            {
                Size grid = GridSize(GrowthScale);
                Size cell = CellSize;
                return new SizeF(grid.Width * cell.Width, grid.Height * cell.Height);
            }
        }

        /// <summary>Where the stem meets the head: <see cref="StemTop"/>, in panel coordinates.</summary>
        public override PointF StemEnd
        {
            get { return _characters == null ? Center : ToPanel(StemTop); }
        }

        /// <summary>Characters to pick from, from empty to brightest.</summary>
        protected virtual string Ramp
        {
            get { return AsciiCanvas.Ramp; }
        }

        /// <summary>Size of one character cell, in pixels.</summary>
        protected virtual Size CellSize
        {
            get { return new Size(GlyphAtlas.CellWidth, GlyphAtlas.CellHeight); }
        }

        /// <summary>Where the stem meets the head, in head units. The seed, sprout, bud and flower all grow from here.</summary>
        protected virtual PointF StemTop
        {
            get { return new PointF(0f, HeadHeight * 0.5f); }
        }

        /// <summary>Height of the head in head units (its width is 1).</summary>
        protected float HeadHeight
        {
            get { return _headHeight; }
        }

        /// <summary>How far the shoot has grown, from 0 (still a seed) to 1 (fully grown).</summary>
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

        /// <summary>How big the bud is, from 0 (no bud yet) to 1 (about to open).</summary>
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

        /// <summary>How open the flower is, from 0 (closed) to 1 (fully open). It opens early in the bloom stage.</summary>
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

        /// <summary>How far through wilting the flower is, from 0 (fresh) to 1 (about to resprout).</summary>
        protected float Wilting
        {
            get { return Stage == GrowthStage.Wilt ? StageProgress : 0f; }
        }

        /// <summary>
        /// Grows and moves like every other flower, then draws the head into its character grid.
        /// When the grid changes size the head is shifted so the end of its stem stays where it was.
        /// </summary>
        public override void Move(Rectangle bounds)
        {
            _ticksAlive++;
            base.Move(bounds);

            PointF stemEndBefore = StemEnd;
            Rasterize();
            PointF stemEndAfter = StemEnd;

            if (stemEndAfter != stemEndBefore)
            {
                Offset(stemEndBefore.X - stemEndAfter.X, stemEndBefore.Y - stemEndAfter.Y);
                KeepInside(bounds);
            }
        }

        /// <summary>Draws the lily's bitmap of characters, bringing it up to date first if the grid has changed.</summary>
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

        /// <summary>Frees the bitmap.</summary>
        public override void Remove()
        {
            if (_bitmap != null)
            {
                _bitmap.Dispose();
                _bitmap = null;
            }
        }

        /// <summary>Samples the head on the character grid and stores the character and colour for every cell.</summary>
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
            float wilt = (float)Math.Round(Wilting * WiltColorSteps) / WiltColorSteps;
            int dataTick = _ticksAlive / DataGlyphTicks;

            for (int row = 0; row < grid.Height; row++)
            {
                for (int column = 0; column < grid.Width; column++)
                {
                    // Measured from the middle so the texture stays on the same part of the head as it grows.
                    int fromCentre = column - grid.Width / 2;
                    int fromMiddle = row - grid.Height / 2;

                    AsciiSample sample = SampleCell(column, row);
                    float brightness = Texture(sample.Intensity, fromCentre, fromMiddle, wilt);
                    int level = (int)Math.Round(brightness * (ramp.Length - 1));
                    char glyph;

                    if (level > 0)
                    {
                        _characters[row, column] = ramp[level];
                        _colors[row, column] = AsciiCanvas.Shade(Blend(sample.Color, WiltColor, wilt * 0.8f), brightness);
                    }
                    else if (IsInsideAura(column, row, grid) &&
                        TryDataGlyph(fromCentre, fromMiddle, dataTick, out glyph))
                    {
                        _characters[row, column] = glyph;
                        _colors[row, column] = AsciiCanvas.Shade(Color, 0f);
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

        /// <summary>The character grid as text, one line per row.</summary>
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

        /// <summary>Called after the character grid has been redrawn.</summary>
        protected virtual void OnRasterized()
        {
        }

        /// <summary>The flower itself, at head point (<paramref name="x"/>, <paramref name="y"/>).</summary>
        protected abstract AsciiSample SampleBloom(float x, float y);

        /// <summary>
        /// A shoot pushing up from <see cref="StemTop"/> with two small leaves at its tip. It shrinks away as the
        /// bud grows in its place.
        /// </summary>
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

        /// <summary>A closed bud on top of the stem that swells, then shrinks away as the flower opens.</summary>
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

            // Faint lines down the bud where the petals overlap.
            float seam = Math.Abs(dx) < 0.15f ? 0.15f : 0f;

            return new AsciiSample(Clamp01((1f - distance) * 3f) * (0.75f + seam), Blend(LeafColor, Color, bud));
        }

        /// <summary>
        /// Brightness of a leaf blade from <paramref name="start"/> to <paramref name="end"/>: pointed at both ends,
        /// widest in the middle, arching upwards by <paramref name="arch"/> of its length, with a bright midrib and
        /// faint veins angled out from it.
        /// </summary>
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

            // Lower the point by the arch instead of raising the leaf, then measure across the straight line.
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

        /// <summary>
        /// Brightness of a shape edge: 1 inside <paramref name="radius"/>, fading to 0 just outside it.
        /// The fade is about the width of one sample, which smooths the edges like anti-aliasing.
        /// </summary>
        protected float Soft(float distance, float radius)
        {
            return AsciiCanvas.Soft(distance, radius, _columnUnit / SamplesAcross);
        }

        /// <summary>Brightness of a line from <paramref name="start"/> to <paramref name="end"/> with round ends.</summary>
        protected float Segment(float x, float y, PointF start, PointF end, float radius)
        {
            return Soft(DistanceToSegment(x, y, start, end), radius);
        }

        /// <summary>Brightness of a filled circle.</summary>
        protected float Disc(float x, float y, PointF centre, float radius)
        {
            float dx = x - centre.X;
            float dy = y - centre.Y;

            return Soft((float)Math.Sqrt(dx * dx + dy * dy), radius);
        }

        /// <summary>Converts a point in head units to panel pixels, using the last rasterized grid.</summary>
        protected PointF ToPanel(PointF point)
        {
            return new PointF(X + (point.X + 0.5f) * _headWidthPixels, Y + point.Y * _headWidthPixels);
        }

        protected static float DistanceToSegment(float x, float y, PointF start, PointF end)
        {
            return AsciiCanvas.DistanceToSegment(x, y, start, end);
        }

        /// <summary>The difference between two angles in radians, wrapped to the range -pi..pi.</summary>
        protected static float AngleBetween(float angle, float other)
        {
            double difference = (angle - other) % (2 * Math.PI);

            if (difference > Math.PI)
            {
                difference -= 2 * Math.PI;
            }
            else if (difference < -Math.PI)
            {
                difference += 2 * Math.PI;
            }

            return (float)difference;
        }

        /// <summary>Mixes two colours: 0 gives <paramref name="from"/>, 1 gives <paramref name="to"/>.</summary>
        protected static Color Blend(Color from, Color to, float amount)
        {
            return AsciiCanvas.Blend(from, to, amount);
        }

        protected static float Clamp01(float value)
        {
            return AsciiCanvas.Clamp01(value);
        }

        /// <summary>
        /// Columns and rows in the grid when the head is at <paramref name="scale"/> of its full-bloom size.
        /// The column count is kept odd so the stem meets the middle column.
        /// </summary>
        private Size GridSize(float scale)
        {
            Size cell = CellSize;

            int columns = (int)Math.Round(_bloomWidth * scale / cell.Width) | 1;
            columns = Math.Max(MinColumns, columns);

            int rows = (int)Math.Round(columns * cell.Width * HeightToWidth / cell.Height);
            rows = Math.Max(MinRows, rows);

            return new Size(columns, rows);
        }

        /// <summary>
        /// Brightness and colour of one cell: the head is sampled at several points inside the cell and the average
        /// is mixed with the brightest point, so thin lines still show while wide shapes are shaded smoothly.
        /// </summary>
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

        /// <summary>Every part of the head at one point: seed, sprout, bud and flower.</summary>
        private AsciiSample SampleHead(float x, float y)
        {
            AsciiSample result = SampleSeed(x, y);
            result = AsciiSample.Brighter(result, SampleSprout(x, y));
            result = AsciiSample.Brighter(result, SampleBud(x, y));
            return AsciiSample.Brighter(result, SampleBloom(x, y));
        }

        /// <summary>The seed at the end of the stem, which swells while it waits and fades as the sprout grows.</summary>
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

        /// <summary>
        /// Adds grain and dark specks to a cell's brightness, and while wilting makes bits of the head drop away
        /// and the rest fade.
        /// </summary>
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

            return Clamp01(brightness * (1f - 0.3f * wilt));
        }

        /// <summary>
        /// Whether a cell is inside the round area around the head where data characters may appear, so they
        /// never fill the corners of the grid and show its square outline.
        /// </summary>
        private bool IsInsideAura(int column, int row, Size grid)
        {
            Size cell = CellSize;
            float dx = (column + 0.5f - grid.Width / 2f) * cell.Width;
            float dy = (row + 0.5f - grid.Height / 2f) * cell.Height;
            float radius = Math.Min(grid.Width * cell.Width, grid.Height * cell.Height) / 2f;

            return dx * dx + dy * dy < radius * radius;
        }

        /// <summary>
        /// Whether an empty cell shows a stray data character right now. They come in short horizontal runs, which
        /// move every few ticks, and there are more of them while the lily is in bloom.
        /// </summary>
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

        /// <summary>Copies the character grid into the bitmap, making the bitmap first if it is too small.</summary>
        private void ComposeBitmap()
        {
            int width = _characters.GetLength(1) * GlyphAtlas.CellWidth;
            int height = _characters.GetLength(0) * GlyphAtlas.CellHeight;

            if (_bitmap == null || _bitmap.Width < width || _bitmap.Height < height)
            {
                // Made big enough for the flower in full bloom, so it rarely has to be made again.
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
