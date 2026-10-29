using System;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// A tiger lily in ASCII: six wide orange petals with dark spots whose tips curl back, and stamens that reach
    /// out past the petals.
    /// Visual state change: the set of characters it is drawn with changes at every growth stage, from a few round
    /// characters for the seed to the full brightness ramp in bloom and soft, curly characters while it wilts.
    /// </summary>
    public class TigerLily : AsciiLily
    {
        // Characters to draw with in each GrowthStage, in order (Seed, Sprout, Bud, Bloom, Wilt), from empty to brightest.
        private static readonly string[] StageRamps =
        {
            " .o",
            " .:;|",
            " .-~=oO",
            " .:-=+*#%@",
            " .,;:~"
        };

        private const int PetalCount = 6;

        // Radius of the open flower, in plant units.
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

            // Angle from the middle of the nearest petal, between -sector/2 and sector/2.
            float nearest = (float)Math.Round(AngleBetween(angle, firstPetal) / sector);
            float fromPetal = AngleBetween(angle, firstPetal + nearest * sector);

            // Each petal is widest in the middle and comes to a point at both sides.
            float petalEdge = radius * (float)Math.Pow(Math.Max(0, Math.Cos(fromPetal * PetalCount / 2)), 0.6);
            AsciiSample result = AsciiSample.Empty;

            if (petalEdge > 0)
            {
                float along = distance / petalEdge;

                // Dark spots scattered over the middle of each petal.
                if (along > 0.25f && along < 0.8f && Math.Sin(distance * 70) * Math.Sin(angle * 14) > 0.6)
                {
                    return new AsciiSample(0.25f, SpotColor);
                }

                // The petal gets brighter towards the tip, where it curls back on itself.
                float petal = Soft(distance, petalEdge) * (0.4f + 0.55f * Clamp01(along) * Clamp01(along));
                result = new AsciiSample(petal, Color);
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
