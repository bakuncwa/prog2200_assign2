using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>
    /// What one point of an <see cref="AsciiLily"/> looks like: how bright it is (0 = empty, 1 = brightest)
    /// and what colour it is.
    /// </summary>
    public struct AsciiSample
    {
        /// <summary>A point with nothing on it.</summary>
        public static readonly AsciiSample Empty = new AsciiSample(0f, Color.Empty);

        public AsciiSample(float intensity, Color color)
        {
            Intensity = intensity;
            Color = color;
        }

        public float Intensity { get; private set; }

        public Color Color { get; private set; }

        /// <summary>Returns whichever sample is brighter, so overlapping parts of a plant combine.</summary>
        public static AsciiSample Brighter(AsciiSample first, AsciiSample second)
        {
            return first.Intensity >= second.Intensity ? first : second;
        }
    }
}
