using System;
using System.Collections;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Makes flowers bounce off each other. Each flower is treated as a circle (<see cref="Flower.Center"/>,
    /// <see cref="Flower.Radius"/>) and every pair is checked once per tick. Flowers react a few pixels before
    /// their circles would touch, so they bounce apart (and wilt) just before they meet. Seeds and sprouts are
    /// skipped: they all start together at the vase mouth and are still too small to bump into anything.
    /// </summary>
    public static class CollisionDetector
    {
        // Below this distance two centres are treated as being on top of each other.
        private const float SameSpotDistance = 0.01f;

        // How many pixels apart two flowers' circles are when they react to each other.
        private const float NearMissMargin = 6f;

        /// <summary>
        /// Checks every pair of flowers in <paramref name="flowers"/> and bounces apart any two that are about to touch.
        /// </summary>
        /// <param name="flowers">An ArrayList that holds only <see cref="Flower"/> objects.</param>
        /// <returns>
        /// An ArrayList of <see cref="PointF"/>: the point between every pair that were both in bloom when they met.
        /// They pollinate each other there, and a new seedling can be planted at that point.
        /// </returns>
        public static ArrayList BounceOffEachOther(ArrayList flowers)
        {
            ArrayList pollinationPoints = new ArrayList();

            for (int i = 0; i < flowers.Count; i++)
            {
                Flower first = (Flower)flowers[i];

                if (IsTooYoung(first))
                {
                    continue;
                }

                for (int j = i + 1; j < flowers.Count; j++)
                {
                    Flower second = (Flower)flowers[j];

                    if (IsTooYoung(second))
                    {
                        continue;
                    }

                    // Checked before bouncing, because a bounce makes both flowers start to wilt.
                    bool bothInBloom = first.Stage == GrowthStage.Bloom && second.Stage == GrowthStage.Bloom;

                    if (Bounce(first, second) && bothInBloom)
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

        private static bool IsTooYoung(Flower flower)
        {
            return flower.Stage == GrowthStage.Seed || flower.Stage == GrowthStage.Sprout;
        }

        /// <summary>
        /// If the two flowers are about to touch, pushes them apart and, when they are moving towards each other,
        /// swaps the parts of their velocities that point along the line between their centres.
        /// This is an elastic collision between two equal masses.
        /// </summary>
        /// <returns>True if they were moving towards each other and bounced.</returns>
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

            // Unit vector from the first centre to the second. Pick any direction if they share a centre.
            float normalX = 1f;
            float normalY = 0f;

            if (distance > SameSpotDistance)
            {
                normalX = dx / distance;
                normalY = dy / distance;
            }

            // Move each flower half the overlap so they are just at the reacting distance.
            float halfOverlap = (reactDistance - distance) / 2;
            first.Offset(-normalX * halfOverlap, -normalY * halfOverlap);
            second.Offset(normalX * halfOverlap, normalY * halfOverlap);

            // How fast they are closing in on each other along that line. Zero or less means they are already separating.
            float closingSpeed = (first.Dx - second.Dx) * normalX + (first.Dy - second.Dy) * normalY;

            if (closingSpeed <= 0)
            {
                return false;
            }

            first.Dx -= closingSpeed * normalX;
            first.Dy -= closingSpeed * normalY;
            second.Dx += closingSpeed * normalX;
            second.Dy += closingSpeed * normalY;

            first.OnCollision();
            second.OnCollision();

            return true;
        }
    }
}
