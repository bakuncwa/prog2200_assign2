using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Shared pieces of the ASCII look: the brightness ramp, shading, a repeatable noise function, and turning a grid
    /// of characters into a bitmap by copying them out of the <see cref="GlyphAtlas"/>.
    /// </summary>
    internal static class AsciiCanvas
    {
        /// <summary>Characters from empty to brightest.</summary>
        public const string Ramp = " .:-=+*#%@";

        // Each cell is sampled on a small grid of points and the results blended, like downsampling an image.
        private const int SamplesAcross = 2;
        private const int SamplesDown = 2;

        /// <summary>
        /// Draws a picture made by <paramref name="sample"/> as ASCII characters and returns it as a bitmap.
        /// <paramref name="sample"/> is given pixel coordinates inside the bitmap and returns how bright and what
        /// colour that point is. Used for pictures that never change shape, like the vase and the hand.
        /// </summary>
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

        /// <summary>
        /// Copies every cell's character out of the glyph atlas, tinted with the cell's colour, into
        /// <paramref name="bitmap"/>. <paramref name="pixels"/> is a work buffer the size of the bitmap.
        /// Anything in the bitmap outside the grid is cleared.
        /// </summary>
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

                            // Premultiplied alpha: each colour channel is already scaled by the coverage.
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

        /// <summary>Dim, normal or highlighted version of a colour depending on how bright the cell is.</summary>
        public static Color Shade(Color color, float brightness)
        {
            if (brightness < 0.4f)
            {
                return Blend(color, Color.Black, 0.45f);
            }

            return brightness < 0.75f ? color : Blend(color, Color.White, 0.35f);
        }

        /// <summary>Mixes two colours: 0 gives <paramref name="from"/>, 1 gives <paramref name="to"/>.</summary>
        public static Color Blend(Color from, Color to, float amount)
        {
            amount = Clamp01(amount);

            return Color.FromArgb(
                (int)Math.Round(from.R + (to.R - from.R) * amount),
                (int)Math.Round(from.G + (to.G - from.G) * amount),
                (int)Math.Round(from.B + (to.B - from.B) * amount));
        }

        /// <summary>
        /// Brightness of a shape edge: 1 inside <paramref name="radius"/>, fading to 0 over
        /// <paramref name="softness"/> around it, which smooths the edges like anti-aliasing.
        /// </summary>
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

        /// <summary>A repeatable pseudo-random number from 0 up to (not including) 1 for three whole numbers.</summary>
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
