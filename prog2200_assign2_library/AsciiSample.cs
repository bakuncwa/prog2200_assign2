using System.Drawing;

namespace prog2200_assign2_library
{
    /// <summary>One point of an ASCII shape: brightness (0 = empty, 1 = brightest) and colour.</summary>
    public struct AsciiSample
    {
        public static readonly AsciiSample Empty = new AsciiSample(0f, Color.Empty);

        public AsciiSample(float intensity, Color color)
        {
            Intensity = intensity;
            Color = color;
        }

        public float Intensity { get; private set; }

        public Color Color { get; private set; }

        /// <summary>Whichever sample is brighter, so overlapping parts combine.</summary>
        public static AsciiSample Brighter(AsciiSample first, AsciiSample second)
        {
            return first.Intensity >= second.Intensity ? first : second;
        }
    }
}
