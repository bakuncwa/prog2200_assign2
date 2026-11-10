using System;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// A tiger lily: six wide spotted orange petals with curled-back tips, and stamens reaching past them.
    /// Rubric (visual state change 1): it is drawn with a different set of characters at each growth stage.
    /// </summary>
    public class TigerLily : AsciiLily
    {
        // Characters for each GrowthStage (Seed, Sprout, Bud, Bloom, Wilt), from empty to brightest.
        private static readonly string[] StageRamps =
        {
            " .o",
            " .:;|",
            " .-~=oO",
            " .:-=+*#%@",
            " .,;:~"
        };

        private const int PetalCount = 6;
        private const float BloomRadius = 0.40f;

        private static readonly Color BloomColor = Color.DarkOrange;
        private static readonly Color SpotColor = Color.Firebrick;
        private static readonly Color StamenColor = Color.Goldenrod;

        public TigerLily(PointF velocity, float bloomWidth)
            : base(velocity, BloomColor, bloomWidth)
        {
        }

        protected override string Ramp
        {
            get { return StageRamps[(int)Stage]; }
        }

        protected override AsciiSample SampleBloom(float x, float y)
        {
            float open = BloomOpen;

            if (open <= 0)
            {
                return AsciiSample.Empty;
            }

            PointF centre = StemTop;
            float dx = x - centre.X;
            float dy = y - centre.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            float angle = (float)Math.Atan2(dy, dx);
            float radius = BloomRadius * open;
            float sector = (float)(2 * Math.PI / PetalCount);
            float firstPetal = (float)(-Math.PI / 2);

            // Each petal is widest in the middle of its sector and pointed at both sides.
            float nearest = (float)Math.Round(AngleBetween(angle, firstPetal) / sector);
            float fromPetal = AngleBetween(angle, firstPetal + nearest * sector);
            float petalEdge = radius * (float)Math.Pow(Math.Max(0, Math.Cos(fromPetal * PetalCount / 2)), 0.6);
            AsciiSample result = AsciiSample.Empty;

            if (petalEdge > 0)
            {
                float along = distance / petalEdge;

                if (along > 0.25f && along < 0.8f && Math.Sin(distance * 70) * Math.Sin(angle * 14) > 0.6)
                {
                    return new AsciiSample(0.25f, SpotColor);
                }

                // Brighter towards the tip, where the petal curls back.
                float towardsTip = AsciiCanvas.Clamp01(along);
                result = new AsciiSample(Soft(distance, petalEdge) * (0.5f + 0.45f * towardsTip * towardsTip), Color);
            }

            for (int i = 0; i < PetalCount; i++)
            {
                float stamenAngle = firstPetal + (i + 0.5f) * sector;
                PointF tip = new PointF(centre.X + (float)Math.Cos(stamenAngle) * radius * 1.1f,
                    centre.Y + (float)Math.Sin(stamenAngle) * radius * 1.1f);
                float stamen = Math.Max(Segment(x, y, centre, tip, 0.008f) * 0.6f, Disc(x, y, tip, 0.025f));

                result = AsciiSample.Brighter(result, new AsciiSample(stamen, StamenColor));
            }

            return result;
        }
    }
}
