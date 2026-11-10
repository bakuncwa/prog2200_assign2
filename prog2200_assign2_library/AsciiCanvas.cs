using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace prog2200_assign2_library
{
    /// <summary>Shared ASCII helpers: the brightness ramp, shading, wilt colours, noise, and grid-to-bitmap copying.</summary>
    internal static class AsciiCanvas
    {
        public const string Ramp = " .:-=+*#%@";

        // A wilting flower turns from its own colour to red, brown, then nearly black, in WiltSteps steps.
        private static readonly Color[] WiltColors =
        {
            Color.FromArgb(200, 40, 35),
            Color.FromArgb(125, 70, 35),
            Color.FromArgb(35, 22, 12)
        };

        private const int WiltSteps = 10;
        private const int SamplesAcross = 2;
        private const int SamplesDown = 2;

        /// <summary>Draws a fixed picture (the vase) as ASCII; <paramref name="sample"/> takes pixel coordinates.</summary>
        public static Bitmap Render(int columns, int rows, Func<float, float, AsciiSample> sample, int noiseSeed)
        {
            char[,] characters = new char[rows, columns];
            Color[,] colors = new Color[rows, columns];

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    float total = 0f;
                    AsciiSample brightest = AsciiSample.Empty;

                    for (int down = 0; down < SamplesDown; down++)
                    {
                        for (int across = 0; across < SamplesAcross; across++)
                        {
                            float x = (column + (across + 0.5f) / SamplesAcross) * GlyphAtlas.CellWidth;
                            float y = (row + (down + 0.5f) / SamplesDown) * GlyphAtlas.CellHeight;
                            AsciiSample point = sample(x, y);

                            total += point.Intensity;
                            brightest = AsciiSample.Brighter(brightest, point);
                        }
                    }

                    float brightness = (total / (SamplesAcross * SamplesDown) + brightest.Intensity) / 2;

                    if (brightness > 0)
                    {
                        brightness = Clamp01(brightness * (0.85f + 0.15f * Hash(column, row, noiseSeed)));
                    }

                    int level = (int)Math.Round(brightness * (Ramp.Length - 1));

                    characters[row, column] = Ramp[level];
                    colors[row, column] = Shade(brightest.Color, brightness);
                }
            }

            Bitmap bitmap = new Bitmap(columns * GlyphAtlas.CellWidth, rows * GlyphAtlas.CellHeight,
                PixelFormat.Format32bppPArgb);
            Blit(bitmap, new int[bitmap.Width * bitmap.Height], characters, colors);

            return bitmap;
        }

        /// <summary>Copies each cell's character from the glyph atlas, tinted with its colour, into the bitmap.</summary>
        public static void Blit(Bitmap bitmap, int[] pixels, char[,] characters, Color[,] colors)
        {
            Array.Clear(pixels, 0, pixels.Length);

            int stride = bitmap.Width;

            for (int row = 0; row < characters.GetLength(0); row++)
            {
                for (int column = 0; column < characters.GetLength(1); column++)
                {
                    if (characters[row, column] == ' ')
                    {
                        continue;
                    }

                    byte[] mask = GlyphAtlas.GetMask(characters[row, column]);
                    Color color = colors[row, column];
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

                            pixels[topLeft + y * stride + x] = (alpha << 24) | (color.R * alpha / 255 << 16) |
                                (color.G * alpha / 255 << 8) | (color.B * alpha / 255);
                        }
                    }
                }
            }

            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);

            try
            {
                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        /// <summary>A dim, normal or highlighted version of a colour, depending on brightness.</summary>
        public static Color Shade(Color color, float brightness)
        {
            if (brightness < 0.4f)
            {
                return Blend(color, Color.Black, 0.45f);
            }

            return brightness < 0.75f ? color : Blend(color, Color.White, 0.35f);
        }

        /// <summary>The colour of something wilting: the original at 0, then red, brown and nearly black at 1.</summary>
        public static Color Wilted(Color original, float wilt)
        {
            float steps = (float)Math.Round(Clamp01(wilt) * WiltSteps) / WiltSteps;

            if (steps <= 0)
            {
                return original;
            }

            float position = steps * WiltColors.Length;
            int next = Math.Min((int)Math.Ceiling(position), WiltColors.Length) - 1;
            Color from = next == 0 ? original : WiltColors[next - 1];

            return Blend(from, WiltColors[next], position - next);
        }

        /// <summary>Mixes two colours: 0 gives from, 1 gives to.</summary>
        public static Color Blend(Color from, Color to, float amount)
        {
            amount = Clamp01(amount);

            return Color.FromArgb(
                (int)Math.Round(from.R + (to.R - from.R) * amount),
                (int)Math.Round(from.G + (to.G - from.G) * amount),
                (int)Math.Round(from.B + (to.B - from.B) * amount));
        }

        /// <summary>1 inside radius, fading to 0 over softness (anti-aliased edges).</summary>
        public static float Soft(float distance, float radius, float softness)
        {
            return Clamp01((radius - distance) / softness + 0.5f);
        }

        public static float DistanceToSegment(float x, float y, PointF start, PointF end)
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

        public static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(value, 1f));
        }

        /// <summary>A repeatable pseudo-random number in [0, 1) for three whole numbers.</summary>
        public static float Hash(int x, int y, int z)
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
