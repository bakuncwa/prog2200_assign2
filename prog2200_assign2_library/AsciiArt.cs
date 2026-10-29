namespace prog2200_assign2_library
{
    /// <summary>
    /// Helpers for the ASCII art flowers, which are written one line per string so the art lines up in the source.
    /// </summary>
    internal static class AsciiArt
    {
        /// <summary>Joins the lines of a piece of ASCII art into one multi-line string.</summary>
        public static string Lines(params string[] lines)
        {
            return string.Join("\n", lines);
        }
    }
}
