using System.Windows.Forms;

namespace prog2200_assign2
{
    /// <summary>
    /// The panel the flowers live in. A plain Panel cannot turn on double buffering from outside
    /// (the property is protected), so this subclass does it.
    /// </summary>
    public class GardenPanel : Panel
    {
        public GardenPanel()
        {
            // Paint into an off-screen buffer first so the flowers do not flicker while they move.
            DoubleBuffered = true;

            // Repaint the whole panel when it is resized, not just the newly uncovered strip.
            ResizeRedraw = true;
        }
    }
}
