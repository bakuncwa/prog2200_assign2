using System.Windows.Forms;

namespace prog2200_assign2_library
{
    /// <summary>
    /// A Windows Forms <see cref="Label"/> that paints into an off-screen buffer first, so changing its text on every
    /// timer tick does not flicker. A plain Label cannot turn on double buffering from outside (the property is
    /// protected), so this subclass does it.
    /// </summary>
    internal class BufferedLabel : Label
    {
        public BufferedLabel()
        {
            // Draw into a buffer and copy it to the screen in one go, and skip the separate background erase.
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }
}
