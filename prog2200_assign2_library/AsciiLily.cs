using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Base class for a lily drawn as ASCII art the way TouchDesigner's ASCII effect works: the plant is
    /// described as a brightness field, the field is sampled on a grid of character cells, and each cell shows
    /// the character from <see cref="Ramp"/> that matches its brightness (dark " " up to bright "@"). The characters
    /// are copied out of a <see cref="GlyphAtlas"/> into one bitmap per lily, which is drawn in a single call.
    /// Every lily grows as a whole plant: a seed in the soil sprouts a stem, leaves unfold, a bud swells on top
    /// and opens into the flower, then the flower crumbles away and browns as it falls, before it resprouts.
    /// Grain, dark specks and stray flickering "data" characters give it the look of a TouchDesigner render.
    /// </summary>
    /// <remarks>
    /// The plant is described in "plant units": x runs from -0.5 (left edge) to 0.5 (right edge) and y runs
    /// down from 0 at the top, using the same scale, so one unit is the plant's full width.
    /// </remarks>
    public abstract class AsciiLily : Flower
    {
        // Characters from empty to brightest.
        private const string DefaultRamp = " .:-=+*#%@";

        // Characters for the stray "data" bits that flicker in the empty space around a lily.
        private const string DataGlyphs = "-.:'|";

        // Plant height compared to its width.
        private const float HeightToWidth = 1.5f;

        // Fewest columns and rows the grid can shrink to, so a seed still shows. Columns must be odd.
        private const int MinColumns = 7;
        private const int MinRows = 5;

        // Each cell is sampled on a small grid of points and the results blended, like downsampling an image.
        private const int SamplesAcross = 2;
        private const int SamplesDown = 2;

        // Stem sway: radians per tick, and how far the top of the stem moves in plant units.
        private const float SwaySpeed = 0.035f;
        private const float SwayAmount = 0.035f;

        // How far the top of the stem droops while the flower wilts, in plant units.
        private const float DroopAcross = 0.14f;
        private const float DroopDown = 0.08f;

        private const float StemRadius = 0.016f;

        // Fraction of cells that get a dark speck, like the texture in a TouchDesigner noise render.
        private const float SpeckChance = 0.08f;

        // How many ticks the stray data characters stay in one place before they jump somewhere else.
        private const int DataGlyphTicks = 6;

        // Wilting colour is shown in steps so the colours stay few.
        private const int WiltColorSteps = 4;

        /// <summary>Colours shared by every lily's stem and leaves: pale greens, like the reference render.</summary>
        protected static readonly Color StemColor = Color.FromArgb(120, 170, 90);
        protected static readonly Color LeafColor = Color.FromArgb(165, 205, 120);

        private static readonly Color SoilColor = Color.FromArgb(150, 110, 70);
        private static readonly Color SeedColor = Color.FromArgb(215, 195, 140);
        private static readonly Color WiltColor = Color.FromArgb(150, 110, 60);

        // Full-bloom width in pixels.
        private readonly float _bloomWidth;

        // Starting point of the sway, so lilies planted together do not sway in step.
        private readonly float _swayPhase;

        // Makes each lily's grain and specks different.
        private readonly int _noiseSeed;

        private char[,] _characters;
        private Color[,] _colors;
        private int _ticksAlive;

        // The characters copied into pixels, and whether that copy is up to date with the character grid.
        private Bitmap _bitmap;
        private int[] _pixels;
        private bool _bitmapIsCurrent;

        // Set each time the plant is rasterized: plant units covered by one column and one row, the plant's height
        // in plant units and its width in pixels.
        private float _columnUnit;
        private float _rowUnit;
        private float _plantHeight;
        private float _plantWidthPixels;

        protected AsciiLily(PointF velocity, Color color, float bloomWidth)
            : base(velocity, color)
        {
            _bloomWidth = bloomWidth;
            _swayPhase = velocity.X + velocity.Y;
            _noiseSeed = (int)(velocity.X * 7919) ^ (int)(velocity.Y * 104729);
        }

        /// <summary>The size of the character grid, which grows with the plant.</summary>
        public override SizeF Size
        {
            get
            {
                Size grid = GridSize(GrowthScale);
                Size cell = CellSize;
                return new SizeF(grid.Width * cell.Width, grid.Height * cell.Height);
            }
        }

        /// <summary>Characters to pick from, from empty to brightest.</summary>
        protected virtual string Ramp
        {
            get { return DefaultRamp; }
        }

        /// <summary>Size of one character cell, in pixels.</summary>
        protected virtual Size CellSize
        {
            get { return new Size(GlyphAtlas.CellWidth, GlyphAtlas.CellHeight); }
        }

        /// <summary>How far down from the top the stem ends (and the flower sits) once it is fully grown, in plant units.</summary>
        protected virtual float StemTopAtFullGrowth
        {
            get { return 0.45f; }
        }

        /// <summary>How far the stem has grown, from 0 (still a seed) to 1 (full height).</summary>
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

        /// <summary>Top of the soil, in plant units. The bottom row of characters is the soil.</summary>
        protected float SoilY
        {
            get { return _plantHeight - _rowUnit; }
        }

        /// <summary>Where the stem currently ends, which is where the bud and flower sit.</summary>
        protected PointF StemTop
        {
            get
            {
                float stemLength = (SoilY - StemTopAtFullGrowth) * StemGrowth;
                float sway = (float)Math.Sin(_ticksAlive * SwaySpeed + _swayPhase) * SwayAmount * StemGrowth;

                return new PointF(sway + Wilting * DroopAcross, SoilY - stemLength + Wilting * DroopDown);
            }
        }

        /// <summary>
        /// Grows and moves like every other flower, then draws the plant into its character grid.
        /// When the grid changes size the plant is shifted so its soil stays put and it grows upwards.
        /// </summary>
        public override void Move(Rectangle bounds)
        {
            SizeF sizeBefore = Size;

            _ticksAlive++;
            base.Move(bounds);
            Rasterize();

            SizeF sizeAfter = Size;

            if (sizeAfter != sizeBefore)
            {
                Offset((sizeBefore.Width - sizeAfter.Width) / 2, sizeBefore.Height - sizeAfter.Height);
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

        /// <summary>Samples the plant on the character grid and stores the character and colour for every cell.</summary>
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
            _plantHeight = grid.Height * _rowUnit;
            _plantWidthPixels = grid.Width * cell.Width;

            string ramp = Ramp;
            float wilt = (float)Math.Round(Wilting * WiltColorSteps) / WiltColorSteps;
            int dataTick = _ticksAlive / DataGlyphTicks;

            for (int row = 0; row < grid.Height; row++)
            {
                for (int column = 0; column < grid.Width; column++)
                {
                    // Measured from the bottom middle so the texture stays on the same part of the plant as it grows.
                    int fromCentre = column - grid.Width / 2;
                    int fromSoil = grid.Height - 1 - row;

                    AsciiSample sample = SampleCell(column, row);
                    float brightness = Texture(sample.Intensity, fromCentre, fromSoil, wilt);
                    int level = (int)Math.Round(brightness * (ramp.Length - 1));
                    char glyph;

                    if (level > 0)
                    {
                        _characters[row, column] = ramp[level];
                        _colors[row, column] = Shade(Blend(sample.Color, WiltColor, wilt * 0.8f), brightness);
                    }
                    else if (TryDataGlyph(fromCentre, fromSoil, dataTick, out glyph))
                    {
                        _characters[row, column] = glyph;
                        _colors[row, column] = Shade(Color, 0f);
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

        /// <summary>The flower itself, at plant point (<paramref name="x"/>, <paramref name="y"/>).</summary>
        protected abstract AsciiSample SampleBloom(float x, float y);

        /// <summary>
        /// A stem rising from the soil to <see cref="StemTop"/>, bending as it sways, with a leaf on each side
        /// (the lower one first) and a small twig near the soil.
        /// </summary>
        protected virtual AsciiSample SampleStem(float x, float y)
        {
            if (StemGrowth <= 0)
            {
                return AsciiSample.Empty;
            }

            PointF top = StemTop;
            float soilY = SoilY;
            AsciiSample result = AsciiSample.Empty;

            if (y >= top.Y && y <= soilY)
            {
                // 0 at the soil and 1 at the top; the stem leans more the higher it goes.
                float along = (soilY - y) / Math.Max(soilY - top.Y, 0.0001f);
                float stemX = top.X * along * along;

                // Slightly brighter nodes every so often along the stem.
                float node = Math.Abs((float)Math.Sin((soilY - y) * 40f));

                result = new AsciiSample(Soft(Math.Abs(x - stemX), StemRadius) * (0.7f + 0.25f * node), StemColor);
            }

            result = AsciiSample.Brighter(result, SampleLeaf(x, y, 0.30f, -1, 0.36f, 0.25f));
            result = AsciiSample.Brighter(result, SampleLeaf(x, y, 0.50f, 1, 0.30f, 0.45f));

            // A thin twig with a tiny leaf, low down on the stem.
            float twigGrowth = Clamp01((StemGrowth - 0.35f) / 0.3f);

            if (twigGrowth > 0)
            {
                PointF twigBase = PointOnStem(0.1f);
                PointF twigTip = new PointF(twigBase.X + 0.16f * twigGrowth, twigBase.Y - 0.06f * twigGrowth);
                float twig = Math.Max(Segment(x, y, twigBase, twigTip, 0.008f) * 0.55f,
                    LeafBlade(x, y, twigTip, new PointF(twigTip.X + 0.1f * twigGrowth, twigTip.Y - 0.01f), 0.015f, 0f));

                result = AsciiSample.Brighter(result, new AsciiSample(twig, StemColor));
            }

            return result;
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

        /// <summary>The point <paramref name="along"/> of the way up the stem (0 = soil, 1 = top).</summary>
        protected PointF PointOnStem(float along)
        {
            PointF top = StemTop;
            float soilY = SoilY;

            return new PointF(top.X * along * along, soilY - (soilY - top.Y) * along);
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
            float softness = _columnUnit / SamplesAcross;

            return Clamp01((radius - distance) / softness + 0.5f);
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

        /// <summary>Converts a point in plant units to panel pixels, using the last rasterized grid.</summary>
        protected PointF ToPanel(PointF point)
        {
            return new PointF(X + (point.X + 0.5f) * _plantWidthPixels, Y + point.Y * _plantWidthPixels);
        }

        protected static float DistanceToSegment(float x, float y, PointF start, PointF end)
        {
            float segmentX = end.X - start.X;
            float segmentY = end.Y - start.Y;
            float lengthSquared = segmentX * segmentX + segmentY * segmentY;
            float along = lengthSquared > 0
                ? Clamp01(((x - start.X) * segmentX + (y - start.Y) * segmentY) / lengthSquared)
                : 0f;
            float dx = x - (start.X + segmentX * along);
            float dy = y - (start.Y + segmentY * along);

            return (float)Math.Sqrt(dx * dx + dy * dy);
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
            amount = Clamp01(amount);

            return Color.FromArgb(
                (int)Math.Round(from.R + (to.R - from.R) * amount),
                (int)Math.Round(from.G + (to.G - from.G) * amount),
                (int)Math.Round(from.B + (to.B - from.B) * amount));
        }

        protected static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(value, 1f));
        }

        /// <summary>
        /// Columns and rows in the grid when the plant is at <paramref name="scale"/> of its full-bloom size.
        /// The column count is kept odd so the stem sits in the middle column.
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
        /// Brightness and colour of one cell: the plant is sampled at several points inside the cell and the average
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
                    AsciiSample sample = SamplePlant(x, y);

                    total += sample.Intensity;
                    brightest = AsciiSample.Brighter(brightest, sample);
                }
            }

            float average = total / (SamplesAcross * SamplesDown);

            return new AsciiSample((average + brightest.Intensity) / 2, brightest.Color);
        }

        /// <summary>Every part of the plant at one point: soil, seed, stem and leaves, bud and flower.</summary>
        private AsciiSample SamplePlant(float x, float y)
        {
            AsciiSample result = SampleSoilAndSeed(x, y);
            result = AsciiSample.Brighter(result, SampleStem(x, y));
            result = AsciiSample.Brighter(result, SampleBud(x, y));
            return AsciiSample.Brighter(result, SampleBloom(x, y));
        }

        /// <summary>
        /// A leaf growing out of the stem <paramref name="along"/> of the way up, reaching out to one side
        /// (-1 = left, 1 = right). It starts to unfold once the stem is <paramref name="startsAt"/> grown.
        /// </summary>
        private AsciiSample SampleLeaf(float x, float y, float along, int side, float length, float startsAt)
        {
            float growth = Clamp01((StemGrowth - startsAt) / 0.35f);

            if (growth <= 0)
            {
                return AsciiSample.Empty;
            }

            PointF leafBase = PointOnStem(along);
            PointF leafTip = new PointF(leafBase.X + side * length * growth, leafBase.Y - length * 0.3f * growth);

            return new AsciiSample(LeafBlade(x, y, leafBase, leafTip, 0.1f * growth, 0.3f), LeafColor);
        }

        /// <summary>A strip of soil along the bottom row, with the seed in it that fades as the sprout grows.</summary>
        private AsciiSample SampleSoilAndSeed(float x, float y)
        {
            AsciiSample result = AsciiSample.Empty;

            if (y > SoilY && Math.Abs(x) < 0.4f)
            {
                result = new AsciiSample(0.35f - 0.5f * Math.Abs(x), SoilColor);
            }

            float seedLeft = 1f - StemGrowth * 2;

            if (seedLeft > 0)
            {
                // The seed swells while it is waiting to sprout.
                float swell = Stage == GrowthStage.Seed ? StageProgress : 1f;
                float seed = Disc(x, y, new PointF(0f, SoilY), 0.08f + 0.06f * swell) * 0.9f * seedLeft;

                result = AsciiSample.Brighter(result, new AsciiSample(seed, SeedColor));
            }

            return result;
        }

        /// <summary>
        /// Adds grain and dark specks to a cell's brightness, and while wilting makes bits of the plant drop away
        /// and the rest fade.
        /// </summary>
        private float Texture(float brightness, int fromCentre, int fromSoil, float wilt)
        {
            if (brightness <= 0)
            {
                return 0f;
            }

            float grain = Hash(fromCentre, fromSoil, _noiseSeed);

            brightness *= 0.8f + 0.2f * grain;

            if (grain < SpeckChance && brightness > 0.35f)
            {
                brightness *= 0.45f;
            }

            if (wilt > 0 && Hash(fromCentre, fromSoil, _noiseSeed + 1) < wilt * 0.7f)
            {
                return 0f;
            }

            return Clamp01(brightness * (1f - 0.3f * wilt));
        }

        /// <summary>
        /// Whether an empty cell shows a stray data character right now. They come in short horizontal runs, which
        /// move every few ticks, and there are more of them while the lily is in bloom.
        /// </summary>
        private bool TryDataGlyph(int fromCentre, int fromSoil, int tick, out char glyph)
        {
            float runChance = 0.02f + 0.05f * BloomOpen;
            bool inRun = Hash(fromCentre >> 2, fromSoil, tick + _noiseSeed) < runChance;

            if (!inRun || Hash(fromCentre, fromSoil, tick) < 0.35f)
            {
                glyph = ' ';
                return false;
            }

            glyph = DataGlyphs[(int)(Hash(fromCentre, fromSoil, tick + 7) * DataGlyphs.Length)];
            return true;
        }

        /// <summary>Copies every cell's character out of the glyph atlas, tinted with the cell's colour, into the bitmap.</summary>
        private void ComposeBitmap()
        {
            int rows = _characters.GetLength(0);
            int columns = _characters.GetLength(1);
            int width = columns * GlyphAtlas.CellWidth;
            int height = rows * GlyphAtlas.CellHeight;

            if (_bitmap == null || _bitmap.Width < width || _bitmap.Height < height)
            {
                // Made big enough for the plant in full bloom, so it rarely has to be made again.
                Size fullGrid = GridSize(1f);

                if (_bitmap != null)
                {
                    _bitmap.Dispose();
                }

                _bitmap = new Bitmap(Math.Max(width, fullGrid.Width * GlyphAtlas.CellWidth),
                    Math.Max(height, fullGrid.Height * GlyphAtlas.CellHeight), PixelFormat.Format32bppPArgb);
                _pixels = new int[_bitmap.Width * _bitmap.Height];
            }

            Array.Clear(_pixels, 0, _pixels.Length);

            int stride = _bitmap.Width;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (_characters[row, column] == ' ')
                    {
                        continue;
                    }

                    byte[] mask = GlyphAtlas.GetMask(_characters[row, column]);
                    Color color = _colors[row, column];
                    int topLeft = row * GlyphAtlas.CellHeight * stride + column * GlyphAtlas.CellWidth;

                    for (int y = 0; y < GlyphAtlas.CellHeight; y++)
                    {
                        for (int x = 0; x < GlyphAtlas.CellWidth; x++)
                        {
                            int alpha = mask[y * GlyphAtlas.CellWidth + x];

                            if (alpha == 0)
                            {
                                continue;
                            }

                            // Premultiplied alpha: each colour channel is already scaled by the coverage.
                            _pixels[topLeft + y * stride + x] = (alpha << 24) | (color.R * alpha / 255 << 16) |
                                (color.G * alpha / 255 << 8) | (color.B * alpha / 255);
                        }
                    }
                }
            }

            BitmapData data = _bitmap.LockBits(new Rectangle(0, 0, _bitmap.Width, _bitmap.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);

            try
            {
                Marshal.Copy(_pixels, 0, data.Scan0, _pixels.Length);
            }
            finally
            {
                _bitmap.UnlockBits(data);
            }

            _bitmapIsCurrent = true;
        }

        /// <summary>Dim, normal or highlighted version of a colour depending on how bright the cell is.</summary>
        private static Color Shade(Color color, float brightness)
        {
            if (brightness < 0.4f)
            {
                return Blend(color, Color.Black, 0.45f);
            }

            return brightness < 0.75f ? color : Blend(color, Color.White, 0.35f);
        }

        /// <summary>A repeatable pseudo-random number from 0 up to (not including) 1 for three whole numbers.</summary>
        private static float Hash(int x, int y, int z)
        {
            unchecked
            {
                uint hash = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791);
                hash ^= hash >> 13;
                hash *= 0x5bd1e995;
                hash ^= hash >> 15;
                return (hash & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
