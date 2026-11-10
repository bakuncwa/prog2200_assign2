using System.Windows.Forms;

namespace prog2200_assign2
{
    /// <summary>
    /// The panel the flowers are drawn on, with double buffering turned on (the property is protected) so the
    /// animation does not flicker.
    /// </summary>
    public class GardenPanel : Panel
    {
        public GardenPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            ResizeRedraw = true;
        }
    }
}
