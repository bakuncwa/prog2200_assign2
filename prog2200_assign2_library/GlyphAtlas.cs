using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Pre-drawn characters for the ASCII lilies, like the font texture TouchDesigner's ASCII effect looks characters
    /// up in. Each character is drawn once into a small cell and kept as a coverage mask (0 = empty, 255 = solid),
    /// so a whole lily can be built by copying masks into a bitmap instead of drawing every character as text.
    /// Characters that are drawn one at a time (the stems) are also kept as small coloured bitmaps.
    /// </summary>
    internal static class GlyphAtlas
    {
        /// <summary>Size of one character cell, in pixels.</summary>
        public const int CellWidth = 5;
        public const int CellHeight = 8;

        private static readonly Font GlyphFont = new Font("Consolas", 7f, FontStyle.Bold);

        private static readonly Dictionary<char, byte[]> Masks = new Dictionary<char, byte[]>();

        // Coloured single-character bitmaps, keyed by the character and the colour's ARGB value.
        private static readonly Dictionary<long, Bitmap> Sprites = new Dictionary<long, Bitmap>();

        /// <summary>
        /// The coverage of every pixel of <paramref name="character"/>'s cell, row by row
        /// (<see cref="CellWidth"/> values per row). Drawn the first time it is asked for.
        /// </summary>
        public static byte[] GetMask(char character)
        {
            byte[] mask;

            if (!Masks.TryGetValue(character, out mask))
            {
                mask = DrawMask(character);
                Masks.Add(character, mask);
            }

            return mask;
        }

        /// <summary>
        /// A cell-sized bitmap of <paramref name="character"/> in <paramref name="color"/> with a transparent background.
        /// Made the first time it is asked for; colours should come from a small set so only a few are ever made.
        /// </summary>
        public static Bitmap GetSprite(char character, Color color)
        {
            long key = ((long)character << 32) | (uint)color.ToArgb();
            Bitmap sprite;

            if (!Sprites.TryGetValue(key, out sprite))
            {
                byte[] mask = GetMask(character);
                sprite = new Bitmap(CellWidth, CellHeight);

                for (int y = 0; y < CellHeight; y++)
                {
                    for (int x = 0; x < CellWidth; x++)
                    {
                        sprite.SetPixel(x, y, Color.FromArgb(mask[y * CellWidth + x], color));
                    }
                }

                Sprites.Add(key, sprite);
            }

            return sprite;
        }

        /// <summary>Draws the character in white on black, centred in a cell, and reads back how bright each pixel is.</summary>
        private static byte[] DrawMask(char character)
        {
            byte[] mask = new byte[CellWidth * CellHeight];
            string text = character.ToString();

            using (Bitmap cell = new Bitmap(CellWidth, CellHeight))
            using (Graphics graphics = Graphics.FromImage(cell))
            {
                graphics.Clear(Color.Black);
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                SizeF size = graphics.MeasureString(text, GlyphFont, PointF.Empty, StringFormat.GenericTypographic);
                PointF origin = new PointF((CellWidth - size.Width) / 2, (CellHeight - size.Height) / 2);

                graphics.DrawString(text, GlyphFont, Brushes.White, origin, StringFormat.GenericTypographic);

                for (int y = 0; y < CellHeight; y++)
                {
                    for (int x = 0; x < CellWidth; x++)
                    {
                        mask[y * CellWidth + x] = cell.GetPixel(x, y).G;
                    }
                }
            }

            return mask;
        }
    }
}
