using System;
using System.Collections;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Makes flowers bounce off each other. Each flower is treated as a circle (<see cref="Flower.Center"/>,
    /// <see cref="Flower.Radius"/>) and every pair is checked once per tick. Flowers react a few pixels before
    /// their circles would touch, so they bounce apart (and blooms wilt) just before they meet. Seeds, sprouts and
    /// wilting flowers are skipped: they are all small and close to the vase mouth, where they would only get in each
    /// other's way growing out of it or shrinking back into it.
    /// </summary>
    public static class CollisionDetector
    {
        // Below this distance two centres are treated as being on top of each other.
        private const float SameSpotDistance = 0.01f;

        // How many pixels apart two flowers' circles are when they react to each other.
        private const float NearMissMargin = 6f;

        // Overlapping flowers are eased apart by at most this many pixels each per tick, so they never jump.
        private const float MaxPushPerTick = 1.5f;

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

                    // Checked before bouncing, because a bounce makes both flowers ready to wilt: only two fresh
                    // blooms meeting for the first time pollinate each other.
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
        /// If the two flowers are about to touch, pushes them apart, tells both they were bumped and, when they are
        /// moving towards each other, swaps the parts of their velocities that point along the line between their
        /// centres. This is an elastic collision between two equal masses. Growing stems can push two heads together
        /// without either moving towards the other, so being close enough is what counts as a collision.
        /// </summary>
        /// <returns>True if they were close enough to collide.</returns>
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

            // Move each flower half the overlap (a little at a time) so they ease apart to the reacting distance.
            float halfOverlap = Math.Min((reactDistance - distance) / 2, MaxPushPerTick);
            first.Offset(-normalX * halfOverlap, -normalY * halfOverlap);
            second.Offset(normalX * halfOverlap, normalY * halfOverlap);

            // How fast they are closing in on each other along that line. Zero or less means they are already separating.
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
