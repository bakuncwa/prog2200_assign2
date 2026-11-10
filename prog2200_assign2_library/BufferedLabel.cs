using System.Windows.Forms;

namespace prog2200_assign2_library
{
    /// <summary>
    /// A standard Label with double buffering turned on (the property is protected), so its text can change every
    /// tick without flicker.
    /// </summary>
    internal class BufferedLabel : Label
    {
        public BufferedLabel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }
}
