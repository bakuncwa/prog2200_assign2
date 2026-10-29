using System;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Draws the stem from the vase mouth to a flower's head in ASCII. The stem is a curve that leaves the vase going
    /// up and reaches the head from below; each character cell it passes through gets a line character that follows
    /// its direction (| / \ -). Once the stem is long enough it grows two shaded leaves on opposite sides.
    /// While its flower wilts, the stem and leaves turn from green to red, brown and then dark.
    /// All stems start from the same point, so together they branch out of the vase like a bouquet.
    /// </summary>
    public static class Stems
    {
        // Pixels between the points the curve is checked at; smaller than a cell so no cell is skipped.
        private const float StepPixels = 2f;

        // Stems shorter than this have no leaves yet.
        private const float LeafStemLength = 60f;

        // Leaf length range in pixels, and its width compared to its length.
        private const float MinLeafLength = 12f;
        private const float MaxLeafLength = 36f;
        private const float LeafWidthToLength = 0.25f;

        private static readonly Color StemColor = Color.FromArgb(120, 170, 90);
        private static readonly Color LeafColor = Color.FromArgb(165, 205, 120);

        /// <summary>Draws the stem from <paramref name="origin"/> to <paramref name="flower"/>'s head.</summary>
        public static void Draw(Graphics graphics, PointF origin, Flower flower)
        {
            PointF end = flower.StemEnd;
            float dx = end.X - origin.X;
            float dy = end.Y - origin.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);

            if (length < GlyphAtlas.CellHeight)
            {
                return;
            }

            Color stemColor = AsciiCanvas.Wilted(StemColor, flower.WiltProgress);

            // The curve leaves the vase going straight up and arrives at the head from below.
            PointF leaving = new PointF(origin.X, origin.Y - length * 0.45f);
            PointF arriving = new PointF(end.X, end.Y + length * 0.3f);

            int steps = (int)(length / StepPixels) + 1;
            Point lastCell = new Point(int.MinValue, int.MinValue);

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                PointF point = Bezier(origin, leaving, arriving, end, t);
                PointF direction = BezierDirection(origin, leaving, arriving, end, t);
                Point cell = new Point((int)Math.Floor(point.X / GlyphAtlas.CellWidth),
                    (int)Math.Floor(point.Y / GlyphAtlas.CellHeight));

                // One character per row where the stem is steep and one per column where it is shallow,
                // so sloping parts are a single clean line instead of a doubled one.
                bool isSteep = Math.Abs(direction.Y) * GlyphAtlas.CellWidth >= Math.Abs(direction.X) * GlyphAtlas.CellHeight;
                bool alreadyDrawn = isSteep ? cell.Y == lastCell.Y : cell.X == lastCell.X;

                if (alreadyDrawn)
                {
                    continue;
                }

                lastCell = cell;
                DrawCell(graphics, cell, LineGlyph(direction), AsciiCanvas.Shade(stemColor, 0.6f));
            }

            if (length >= LeafStemLength)
            {
                Color leafColor = AsciiCanvas.Wilted(LeafColor, flower.WiltProgress);
                float leafLength = Math.Max(MinLeafLength, Math.Min(MaxLeafLength, length * 0.18f));

                DrawLeaf(graphics, origin, leaving, arriving, end, 0.3f, -1, leafLength, leafColor);
                DrawLeaf(graphics, origin, leaving, arriving, end, 0.55f, 1, leafLength * 0.85f, leafColor);
            }
        }

        /// <summary>
        /// A leaf growing out of the stem at <paramref name="along"/> of its length, on one side (-1 or 1), tilted
        /// upwards and shaded like the lilies' leaves: brightest along its midrib, fading towards its edges.
        /// </summary>
        private static void DrawLeaf(Graphics graphics, PointF p0, PointF p1, PointF p2, PointF p3, float along,
            int side, float length, Color color)
        {
            PointF leafBase = Bezier(p0, p1, p2, p3, along);
            PointF direction = BezierDirection(p0, p1, p2, p3, along);
            float directionLength = Math.Max((float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y),
                0.0001f);

            // Out to the side, tilted up.
            float outX = -direction.Y / directionLength * side;
            float outY = direction.X / directionLength * side - 0.7f;
            float outLength = (float)Math.Sqrt(outX * outX + outY * outY);
            PointF tip = new PointF(leafBase.X + outX / outLength * length, leafBase.Y + outY / outLength * length);
            float halfWidth = length * LeafWidthToLength;

            int left = (int)Math.Floor((Math.Min(leafBase.X, tip.X) - halfWidth) / GlyphAtlas.CellWidth);
            int right = (int)Math.Floor((Math.Max(leafBase.X, tip.X) + halfWidth) / GlyphAtlas.CellWidth);
            int top = (int)Math.Floor((Math.Min(leafBase.Y, tip.Y) - halfWidth) / GlyphAtlas.CellHeight);
            int bottom = (int)Math.Floor((Math.Max(leafBase.Y, tip.Y) + halfWidth) / GlyphAtlas.CellHeight);

            for (int row = top; row <= bottom; row++)
            {
                for (int column = left; column <= right; column++)
                {
                    float x = (column + 0.5f) * GlyphAtlas.CellWidth;
                    float y = (row + 0.5f) * GlyphAtlas.CellHeight;
                    float brightness = LeafBrightness(x, y, leafBase, tip, halfWidth);
                    int level = (int)Math.Round(brightness * (AsciiCanvas.Ramp.Length - 1));

                    if (level > 0)
                    {
                        DrawCell(graphics, new Point(column, row), AsciiCanvas.Ramp[level],
                            AsciiCanvas.Shade(color, brightness));
                    }
                }
            }
        }

        /// <summary>Brightness of a pointed, slightly arched leaf blade at pixel (<paramref name="x"/>, <paramref name="y"/>).</summary>
        private static float LeafBrightness(float x, float y, PointF start, PointF end, float halfWidth)
        {
            float axisX = end.X - start.X;
            float axisY = end.Y - start.Y;
            float length = (float)Math.Sqrt(axisX * axisX + axisY * axisY);
            float unitX = axisX / length;
            float unitY = axisY / length;
            float along = ((x - start.X) * unitX + (y - start.Y) * unitY) / length;

            if (along <= 0 || along >= 1)
            {
                return 0f;
            }

            float bow = 0.2f * length * (float)Math.Sin(Math.PI * along);
            float across = Math.Abs(-(x - start.X) * unitY + (y + bow - start.Y) * unitX);
            float half = halfWidth * (float)Math.Pow(Math.Sin(Math.PI * along), 0.7);
            float edge = AsciiCanvas.Soft(across, half, GlyphAtlas.CellWidth * 0.6f);

            return edge * (0.5f + 0.4f * (1f - across / Math.Max(half, 0.0001f)));
        }

        /// <summary>The line character that best follows <paramref name="direction"/> on screen.</summary>
        private static char LineGlyph(PointF direction)
        {
            float across = Math.Abs(direction.X);
            float down = Math.Abs(direction.Y);

            if (across < down * 0.4f)
            {
                return '|';
            }

            if (down < across * 0.4f)
            {
                return '-';
            }

            // Screen y points down, so moving right and up is "/".
            return direction.X * direction.Y < 0 ? '/' : '\\';
        }

        private static void DrawCell(Graphics graphics, Point cell, char glyph, Color color)
        {
            Rectangle area = new Rectangle(cell.X * GlyphAtlas.CellWidth, cell.Y * GlyphAtlas.CellHeight,
                GlyphAtlas.CellWidth, GlyphAtlas.CellHeight);

            graphics.DrawImage(GlyphAtlas.GetSprite(glyph, color), area);
        }

        /// <summary>A point on the cubic Bezier curve p0-p1-p2-p3 at <paramref name="t"/> (0 = p0, 1 = p3).</summary>
        private static PointF Bezier(PointF p0, PointF p1, PointF p2, PointF p3, float t)
        {
            float u = 1 - t;
            float a = u * u * u;
            float b = 3 * u * u * t;
            float c = 3 * u * t * t;
            float d = t * t * t;

            return new PointF(a * p0.X + b * p1.X + c * p2.X + d * p3.X, a * p0.Y + b * p1.Y + c * p2.Y + d * p3.Y);
        }

        /// <summary>Which way the curve is heading at <paramref name="t"/> (not normalised).</summary>
        private static PointF BezierDirection(PointF p0, PointF p1, PointF p2, PointF p3, float t)
        {
            float u = 1 - t;

            return new PointF(
                3 * u * u * (p1.X - p0.X) + 6 * u * t * (p2.X - p1.X) + 3 * t * t * (p3.X - p2.X),
                3 * u * u * (p1.Y - p0.Y) + 6 * u * t * (p2.Y - p1.Y) + 3 * t * t * (p3.Y - p2.Y));
        }
    }
}
