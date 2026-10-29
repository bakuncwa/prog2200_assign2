using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Base class for a lily drawn as ASCII art the way TouchDesigner's ASCII effect works: the plant is
    /// described as a brightness field, the field is sampled on a grid of character cells, and each cell shows
    /// the character from <see cref="Ramp"/> that matches its brightness (dark " " up to bright "@").
    /// Every lily grows as a whole plant: a seed in the soil sprouts a stem, leaves unfold, a bud swells on top
    /// and opens into the flower, then the flower droops and browns before it reseeds.
    /// </summary>
    /// <remarks>
    /// The plant is described in "plant units": x runs from -0.5 (left edge) to 0.5 (right edge) and y runs
    /// down from 0 at the top, using the same scale, so one unit is the plant's full width.
    /// </remarks>
    public abstract class AsciiLily : Flower
    {
        // Characters from empty to brightest.
        private const string DefaultRamp = " .:-=+*#%@";

        // Plant height compared to its width.
        private const float HeightToWidth = 1.35f;

        // Fewest columns and rows the grid can shrink to, so a seed still shows.
        private const int MinColumns = 5;
        private const int MinRows = 3;

        // Each cell is sampled on a small grid of points and the results blended, like downsampling an image.
        private const int SamplesAcross = 2;
        private const int SamplesDown = 3;

        // Rows are drawn a little closer together than the font's line height so the cells are closer to square.
        private const float RowSpacing = 0.8f;

        // Stem sway: radians per tick, and how far the top of the stem moves in plant units.
        private const float SwaySpeed = 0.04f;
        private const float SwayAmount = 0.04f;

        // How far the top of the stem droops while the flower wilts, in plant units.
        private const float DroopAcross = 0.16f;
        private const float DroopDown = 0.10f;

        private const float StemRadius = 0.025f;
        private const float LeafRadius = 0.03f;

        // Wilting colour is shown in steps so neighbouring cells share a colour and can be drawn together.
        private const int WiltColorSteps = 4;

        private const TextFormatFlags CellFormat = TextFormatFlags.NoPadding;

        /// <summary>Font every lily's characters are drawn in. It must be monospaced so the cells line up.</summary>
        protected static readonly Font GridFont = new Font("Consolas", 7f, FontStyle.Bold);

        /// <summary>Width and height of one character of <see cref="GridFont"/>, in pixels.</summary>
        protected static readonly Size CharacterSize = MeasureCharacter();

        /// <summary>Colours shared by every lily's stem and leaves.</summary>
        protected static readonly Color StemColor = Color.ForestGreen;
        protected static readonly Color LeafColor = Color.YellowGreen;

        private static readonly Color SoilColor = Color.Sienna;
        private static readonly Color SeedColor = Color.BurlyWood;
        private static readonly Color WiltColor = Color.FromArgb(150, 110, 60);

        // Full-bloom width in pixels.
        private readonly float _bloomWidth;

        // Starting point of the sway, so lilies planted together do not sway in step.
        private readonly float _swayPhase;

        private char[,] _characters;
        private Color[,] _colors;
        private int _ticksAlive;

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
        }

        /// <summary>The size of the character grid, which grows with the plant.</summary>
        public override SizeF Size
        {
            get
            {
                Size grid = GridSize();
                return new SizeF(grid.Width * CharacterSize.Width, grid.Height * RowHeight);
            }
        }

        /// <summary>Characters to pick from, from empty to brightest.</summary>
        protected virtual string Ramp
        {
            get { return DefaultRamp; }
        }

        /// <summary>Pixels from one row of characters to the next.</summary>
        protected virtual int RowHeight
        {
            get { return (int)Math.Round(CharacterSize.Height * RowSpacing); }
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

        /// <summary>How far through wilting the flower is, from 0 (fresh) to 1 (about to drop its seed).</summary>
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

        /// <summary>
        /// Draws the character grid. Neighbouring characters of the same colour are drawn as one string,
        /// which is much faster than drawing every character on its own.
        /// </summary>
        public override void Draw(Graphics graphics)
        {
            if (_characters == null)
            {
                return;
            }

            int rows = _characters.GetLength(0);
            int columns = _characters.GetLength(1);
            int rowHeight = RowHeight;
            char[] run = new char[columns];

            for (int row = 0; row < rows; row++)
            {
                int column = 0;

                while (column < columns)
                {
                    if (_characters[row, column] == ' ')
                    {
                        column++;
                        continue;
                    }

                    int start = column;
                    int last = column;
                    int runColor = _colors[row, column].ToArgb();

                    // Spaces do not show a colour, so they can sit inside a run of any colour.
                    while (column < columns &&
                        (_characters[row, column] == ' ' || _colors[row, column].ToArgb() == runColor))
                    {
                        run[column - start] = _characters[row, column];

                        if (_characters[row, column] != ' ')
                        {
                            last = column;
                        }

                        column++;
                    }

                    Point position = Point.Round(new PointF(X + start * CharacterSize.Width, Y + row * rowHeight));

                    TextRenderer.DrawText(graphics, new string(run, 0, last - start + 1), GridFont, position,
                        _colors[row, start], CellFormat);
                }
            }
        }

        /// <summary>Samples the plant on the character grid and stores the character and colour for every cell.</summary>
        protected void Rasterize()
        {
            Size grid = GridSize();

            if (_characters == null || _characters.GetLength(0) != grid.Height || _characters.GetLength(1) != grid.Width)
            {
                _characters = new char[grid.Height, grid.Width];
                _colors = new Color[grid.Height, grid.Width];
            }

            _columnUnit = 1f / grid.Width;
            _rowUnit = _columnUnit * RowHeight / CharacterSize.Width;
            _plantHeight = grid.Height * _rowUnit;
            _plantWidthPixels = grid.Width * CharacterSize.Width;

            string ramp = Ramp;
            float wilt = (float)Math.Round(Wilting * WiltColorSteps) / WiltColorSteps;

            for (int row = 0; row < grid.Height; row++)
            {
                for (int column = 0; column < grid.Width; column++)
                {
                    AsciiSample cell = SampleCell(column, row);
                    float brightness = Clamp01(cell.Intensity * (1f - 0.3f * wilt));
                    int level = (int)Math.Round(brightness * (ramp.Length - 1));

                    _characters[row, column] = ramp[level];
                    _colors[row, column] = Blend(cell.Color, WiltColor, wilt * 0.8f);
                }
            }

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

        /// <summary>A stem rising from the soil to <see cref="StemTop"/>, bending as it sways, with a leaf on each side.</summary>
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

                result = new AsciiSample(Soft(Math.Abs(x - stemX), StemRadius) * 0.75f, StemColor);
            }

            // Leaves unfold once the stem is a quarter grown.
            float leafGrowth = Clamp01((StemGrowth - 0.25f) / 0.4f);

            if (leafGrowth > 0)
            {
                PointF leafBase = new PointF(top.X * 0.09f, soilY - (soilY - top.Y) * 0.3f);

                for (int side = -1; side <= 1; side += 2)
                {
                    PointF leafTip = new PointF(leafBase.X + side * 0.22f * leafGrowth, leafBase.Y - 0.14f * leafGrowth);
                    float leaf = Segment(x, y, leafBase, leafTip, LeafRadius) * 0.55f;

                    result = AsciiSample.Brighter(result, new AsciiSample(leaf, LeafColor));
                }
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

            return new AsciiSample(Clamp01((1f - distance) * 3f) * 0.9f, Blend(LeafColor, Color, bud));
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
        /// Columns and rows in the grid right now. The column count is kept odd so the stem sits in the middle column.
        /// </summary>
        private Size GridSize()
        {
            int columns = (int)Math.Round(_bloomWidth * GrowthScale / CharacterSize.Width) | 1;
            columns = Math.Max(MinColumns, columns);

            int rows = (int)Math.Round(columns * CharacterSize.Width * HeightToWidth / RowHeight);
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

        private static Size MeasureCharacter()
        {
            const string sample = "MMMMMMMMMM";
            Size size = TextRenderer.MeasureText(sample, GridFont, System.Drawing.Size.Empty, CellFormat);

            return new Size((int)Math.Round(size.Width / (float)sample.Length), size.Height);
        }
    }
}
