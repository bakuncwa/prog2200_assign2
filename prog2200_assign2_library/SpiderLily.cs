using System;
using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// A spider lily: six thin curling petals with pollen-tipped stamens between them.
    /// The colour gradient around the flower turns every tick, so the colour spins.
    /// </summary>
    public class SpiderLily : AsciiLily
    {
        private const float GradientTurnPerTick = 3f;
        private const int GradientSteps = 4;
        private const int PetalCount = 6;
        private const float BloomRadius = 0.42f;
        private const float PetalCurl = 0.7f;

        private static readonly Color TipColor = Color.White;

        private float _gradientAngle;

        public SpiderLily(PointF velocity, Color color, float bloomWidth)
            : base(velocity, color, bloomWidth)
        {
        }

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

                // Petal: a tapering curve that bends round as it goes out, with a brighter midrib.
                if (distance < petalLength)
                {
                    float along = distance / petalLength;
                    float centreLine = petalAngle + PetalCurl * along * along;
                    float sideways = Math.Abs(AngleBetween(angle, centreLine)) * distance;
                    float width = 0.045f * (1f - along) + 0.01f;
                    float midrib = sideways < width * 0.3f ? 0.2f : 0f;

                    intensity = Math.Max(intensity, Soft(sideways, width) * (0.8f - 0.45f * along + midrib));
                }

                // Stamen: a thin line between two petals with a pollen dot on the end.
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

            // Visual state change: colour depends on the angle around the flower relative to the turning gradient.
            double gradientRadians = _gradientAngle * Math.PI / 180;
            float shade = 0.5f + 0.5f * (float)Math.Cos(angle - gradientRadians);
            int step = Math.Min(GradientSteps - 1, (int)(shade * GradientSteps));

            return new AsciiSample(intensity, AsciiCanvas.Blend(Color, TipColor, 0.8f * step / (GradientSteps - 1)));
        }
    }
}
