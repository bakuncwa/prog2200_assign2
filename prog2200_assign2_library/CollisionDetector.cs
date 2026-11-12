using System;
using System.Collections;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Makes flower heads bounce off each other. Each head is a circle
    /// (<see cref="Flower.Center"/>, <see cref="Flower.Radius"/>) and every pair is checked once per tick.
    /// Seeds, sprouts and wilting flowers are skipped because they sit together at the vase mouth.
    /// </summary>
    public static class CollisionDetector
    {
        private const float SameSpotDistance = 0.01f;

        // Heads react this many pixels before they would touch, and are eased apart at most this far per tick.
        private const float NearMissMargin = 6f;
        private const float MaxPushPerTick = 1.5f;

        /// <summary>Bounces apart every pair of heads that are about to touch.</summary>
        /// <param name="flowers">An ArrayList of Flower objects; each item is cast explicitly.</param>
        /// <returns>An ArrayList of PointF: where two fresh blooms met (a new lily is planted for each).</returns>
        public static ArrayList BounceOffEachOther(ArrayList flowers)
        {
            ArrayList pollinationPoints = new ArrayList();

            for (int i = 0; i < flowers.Count; i++)
            {
                Flower first = (Flower)flowers[i];

                if (IsSkipped(first))
                {
                    continue;
                }

                for (int j = i + 1; j < flowers.Count; j++)
                {
                    Flower second = (Flower)flowers[j];

                    if (IsSkipped(second))
                    {
                        continue;
                    }

                    // Checked before bouncing, because a bounce makes both blooms ready to wilt.
                    bool freshBlooms = first.Stage == GrowthStage.Bloom && second.Stage == GrowthStage.Bloom &&
                        !first.IsReadyToWilt && !second.IsReadyToWilt;

                    if (Bounce(first, second) && freshBlooms)
                    {
                        PointF firstCenter = first.Center;
                        PointF secondCenter = second.Center;

                        pollinationPoints.Add((object)new PointF((firstCenter.X + secondCenter.X) / 2,
                            (firstCenter.Y + secondCenter.Y) / 2));
                    }
                }
            }

            return pollinationPoints;
        }

        private static bool IsSkipped(Flower flower)
        {
            return flower.Stage == GrowthStage.Seed || flower.Stage == GrowthStage.Sprout ||
                flower.Stage == GrowthStage.Wilt;
        }

        /// <summary>
        /// If two heads are about to touch, eases them apart and, if they are moving towards each other, swaps their
        /// velocity components along the line between their centres (an elastic collision of equal masses).
        /// </summary>
        private static bool Bounce(Flower first, Flower second)
        {
            PointF firstCenter = first.Center;
            PointF secondCenter = second.Center;
            float dx = secondCenter.X - firstCenter.X;
            float dy = secondCenter.Y - firstCenter.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            float reactDistance = first.Radius + second.Radius + NearMissMargin;

            if (distance >= reactDistance)
            {
                return false;
            }

            float normalX = 1f;
            float normalY = 0f;

            if (distance > SameSpotDistance)
            {
                normalX = dx / distance;
                normalY = dy / distance;
            }

            float halfOverlap = Math.Min((reactDistance - distance) / 2, MaxPushPerTick);
            first.Offset(-normalX * halfOverlap, -normalY * halfOverlap);
            second.Offset(normalX * halfOverlap, normalY * halfOverlap);

            float closingSpeed = (first.Dx - second.Dx) * normalX + (first.Dy - second.Dy) * normalY;

            if (closingSpeed > 0)
            {
                first.Dx -= closingSpeed * normalX;
                first.Dy -= closingSpeed * normalY;
                second.Dx += closingSpeed * normalX;
                second.Dy += closingSpeed * normalY;
            }

            first.OnCollision();
            second.OnCollision();

            return true;
        }
    }
}
