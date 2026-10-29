using System;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// A spider lily in ASCII: six thin petals that curl round from the centre, with long stamens tipped with pollen
    /// reaching out between them.
    /// Visual state change: the flower is coloured by a gradient around its centre whose angle turns on every tick,
    /// so the colour appears to spin around the flower.
    /// </summary>
    public class SpiderLily : AsciiLily
    {
        // Degrees the gradient turns each tick.
        private const float GradientTurnPerTick = 6f;

        private const int PetalCount = 6;

        // Radius of the open flower, in plant units.
        private const float BloomRadius = 0.42f;

        // How far each petal curls round by its tip, in radians.
        private const float PetalCurl = 0.7f;

        // The gradient is shown in steps so neighbouring characters share a colour and can be drawn together.
        private const int GradientSteps = 4;

        private static readonly Color TipColor = Color.White;

        private float _gradientAngle;

        public SpiderLily(PointF velocity, Color color, float bloomWidth)
            : base(velocity, color, bloomWidth)
        {
        }

        /// <summary>Turns the gradient, then grows and moves like every other lily.</summary>
        public override void Move(Rectangle bounds)
        {
            _gradientAngle = (_gradientAngle + GradientTurnPerTick) % 360f;
            base.Move(bounds);
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
            float petalLength = radius * 0.8f;
            float intensity = Disc(x, y, centre, 0.05f);

            for (int i = 0; i < PetalCount; i++)
            {
                float petalAngle = (float)(i * 2 * Math.PI / PetalCount - Math.PI / 2);

                // A petal is a thin, tapering curve that bends round as it goes out, brightest near the centre.
                if (distance < petalLength)
                {
                    float along = distance / petalLength;
                    float centreLine = petalAngle + PetalCurl * along * along;
                    float sideways = Math.Abs(AngleBetween(angle, centreLine)) * distance;
                    float width = 0.045f * (1f - along) + 0.01f;

                    intensity = Math.Max(intensity, Soft(sideways, width) * (1f - 0.45f * along));
                }

                // A stamen is a thin straight line halfway between two petals, with a bright pollen dot on the end.
                float stamenAngle = (float)(petalAngle + Math.PI / PetalCount);
                PointF tip = new PointF(centre.X + (float)Math.Cos(stamenAngle) * radius,
                    centre.Y + (float)Math.Sin(stamenAngle) * radius);

                intensity = Math.Max(intensity, Segment(x, y, centre, tip, 0.008f) * 0.5f);
                intensity = Math.Max(intensity, Disc(x, y, tip, 0.03f));
            }

            if (intensity <= 0)
            {
                return AsciiSample.Empty;
            }

            double gradientRadians = _gradientAngle * Math.PI / 180;
            float shade = 0.5f + 0.5f * (float)Math.Cos(angle - gradientRadians);
            int step = Math.Min(GradientSteps - 1, (int)(shade * GradientSteps));

            return new AsciiSample(intensity, Blend(Color, TipColor, 0.8f * step / (GradientSteps - 1)));
        }
    }
}
