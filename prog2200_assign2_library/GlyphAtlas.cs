using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Each character drawn once into a 5 x 8 cell and cached: as a coverage mask (for building lily bitmaps) and as
    /// small coloured bitmaps (for the stems), so nothing is drawn as text every frame.
    /// </summary>
    internal static class GlyphAtlas
    {
        public const int CellWidth = 5;
        public const int CellHeight = 8;

        private static readonly Font GlyphFont = new Font("Consolas", 7f, FontStyle.Bold);

        private static readonly Dictionary<char, byte[]> Masks = new Dictionary<char, byte[]>();

        // Keyed by the character and the colour's ARGB value.
        private static readonly Dictionary<long, Bitmap> Sprites = new Dictionary<long, Bitmap>();

        /// <summary>Coverage (0-255) of each pixel of the character's cell, row by row.</summary>
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

        /// <summary>A cell-sized bitmap of the character in the given colour, on a transparent background.</summary>
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

        /// <summary>Draws the character white on black, centred in a cell, and reads back each pixel's brightness.</summary>
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
