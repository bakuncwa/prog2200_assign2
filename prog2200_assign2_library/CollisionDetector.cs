using System;
using System.Collections;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// Makes flowers bounce off each other. Each flower is treated as a circle (<see cref="Flower.Center"/>,
    /// <see cref="Flower.Radius"/>) and every pair is checked once per tick.
    /// </summary>
    public static class CollisionDetector
    {
        // Below this distance two centres are treated as being on top of each other.
        private const float SameSpotDistance = 0.01f;

        /// <summary>
        /// Checks every pair of flowers in <paramref name="flowers"/> and bounces apart any two that overlap.
        /// </summary>
        /// <param name="flowers">An ArrayList that holds only <see cref="Flower"/> objects.</param>
        public static void BounceOffEachOther(ArrayList flowers)
        {
            for (int i = 0; i < flowers.Count; i++)
            {
                Flower first = (Flower)flowers[i];

                for (int j = i + 1; j < flowers.Count; j++)
                {
                    Flower second = (Flower)flowers[j];
                    Bounce(first, second);
                }
            }
        }

        /// <summary>
        /// If the two flowers overlap, pushes them apart and, when they are moving towards each other,
        /// swaps the parts of their velocities that point along the line between their centres.
        /// This is an elastic collision between two equal masses.
        /// </summary>
        private static void Bounce(Flower first, Flower second)
        {
            PointF firstCenter = first.Center;
            PointF secondCenter = second.Center;

            float dx = secondCenter.X - firstCenter.X;
            float dy = secondCenter.Y - firstCenter.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            float touchingDistance = first.Radius + second.Radius;

            if (distance >= touchingDistance)
            {
                return;
            }

            // Unit vector from the first centre to the second. Pick any direction if they share a centre.
            float normalX = 1f;
            float normalY = 0f;

            if (distance > SameSpotDistance)
            {
                normalX = dx / distance;
                normalY = dy / distance;
            }

            // Move each flower half the overlap so they are just touching.
            float halfOverlap = (touchingDistance - distance) / 2;
            first.Offset(-normalX * halfOverlap, -normalY * halfOverlap);
            second.Offset(normalX * halfOverlap, normalY * halfOverlap);

            // How fast they are closing in on each other along that line. Zero or less means they are already separating.
            float closingSpeed = (first.Dx - second.Dx) * normalX + (first.Dy - second.Dy) * normalY;

            if (closingSpeed <= 0)
            {
                return;
            }

            first.Dx -= closingSpeed * normalX;
            first.Dy -= closingSpeed * normalY;
            second.Dx += closingSpeed * normalX;
            second.Dy += closingSpeed * normalY;

            first.OnCollision();
            second.OnCollision();
        }
    }
}
