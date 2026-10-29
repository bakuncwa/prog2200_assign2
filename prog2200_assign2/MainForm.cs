using System;
using System.Collections;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using prog2200_assign2_library;

namespace prog2200_assign2
{
    /// <summary>
    /// The ASCII Lily Garden screen saver window. All lily shapes live in prog2200_assign2_library;
    /// this form keeps them in an ArrayList and processes them polymorphically: on every timer tick each item
    /// is cast back to <see cref="Flower"/> and told to Move, and on every repaint it is told to Draw.
    /// Which Move and Draw actually run is decided at run time by the flower's real type (late binding).
    /// </summary>
    public partial class MainForm : Form
    {
        // Used when App.config does not set a value.
        private const int DefaultTickIntervalMs = 30;
        private const int DefaultStartingFlowersPerKind = 2;

        // Full-bloom width range for the lilies, in pixels.
        private const int MinBloomSize = 70;
        private const int MaxBloomSize = 110;

        // Speed range, in pixels per tick.
        private const double MinSpeed = 1.5;
        private const double MaxSpeed = 4.0;

        private const string RunningHint = "Click the garden to plant a random lily";
        private const string PausedHint = "Paused";

        // Spider lilies come in reds, pinks, golds and white. The other lilies choose their own colours.
        private static readonly Color[] SpiderLilyColors =
        {
            Color.Crimson,
            Color.OrangeRed,
            Color.HotPink,
            Color.Gold,
            Color.White,
            Color.LightCoral
        };

        // Every flower in the garden. Each one is explicitly upcast to Flower (and then to object) when it is added,
        // and explicitly cast back to Flower whenever it is used.
        private readonly ArrayList _flowers = new ArrayList();

        private readonly Random _random = new Random();

        // Read from the appSettings section of App.config.
        private readonly int _startingFlowersPerKind = ReadSetting("StartingFlowersPerKind", DefaultStartingFlowersPerKind);

        // How the window looked before going full screen, so it can be put back.
        private FormBorderStyle _windowedBorderStyle;
        private FormWindowState _windowedState;
        private bool _isFullScreen;

        private enum FlowerKind
        {
            SpiderLily,
            CallaLily,
            TigerLily,
            LilyOfTheValley
        }

        public MainForm()
        {
            InitializeComponent();

            animationTimer.Interval = ReadSetting("TickIntervalMs", DefaultTickIntervalMs);
        }

        /// <summary>
        /// Plants the first flowers and starts the animation. Done here rather than in the constructor
        /// because the panel only has its real size once the window is on screen.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            for (int i = 0; i < _startingFlowersPerKind; i++)
            {
                foreach (FlowerKind kind in Enum.GetValues(typeof(FlowerKind)))
                {
                    AddFlower(kind, RandomPointInGarden());
                }
            }

            animationTimer.Start();
            UpdateStatus();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            animationTimer.Stop();

            base.OnFormClosing(e);
        }

        /// <summary>Lets Esc leave full screen, which is easier to find than F11.</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && _isFullScreen)
            {
                SetFullScreen(false);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        #region Menu handlers

        private void addSpiderLilyMenuItem_Click(object sender, EventArgs e)
        {
            AddFlower(FlowerKind.SpiderLily, RandomPointInGarden());
        }

        private void addCallaLilyMenuItem_Click(object sender, EventArgs e)
        {
            AddFlower(FlowerKind.CallaLily, RandomPointInGarden());
        }

        private void addTigerLilyMenuItem_Click(object sender, EventArgs e)
        {
            AddFlower(FlowerKind.TigerLily, RandomPointInGarden());
        }

        private void addLilyOfTheValleyMenuItem_Click(object sender, EventArgs e)
        {
            AddFlower(FlowerKind.LilyOfTheValley, RandomPointInGarden());
        }

        private void addRandomMenuItem_Click(object sender, EventArgs e)
        {
            AddFlower(RandomKind(), RandomPointInGarden());
        }

        private void pauseMenuItem_Click(object sender, EventArgs e)
        {
            animationTimer.Enabled = !animationTimer.Enabled;
            pauseMenuItem.Text = animationTimer.Enabled ? "&Pause" : "&Resume";
            UpdateStatus();
        }

        private void clearMenuItem_Click(object sender, EventArgs e)
        {
            foreach (object item in _flowers)
            {
                Flower flower = (Flower)item;
                flower.Remove();
            }

            _flowers.Clear();
            gardenPanel.Invalidate();
            UpdateStatus();
        }

        private void exitMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void fullScreenMenuItem_Click(object sender, EventArgs e)
        {
            SetFullScreen(!_isFullScreen);
        }

        #endregion

        #region Garden events

        /// <summary>Plants a random lily wherever the garden is clicked.</summary>
        private void gardenPanel_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                AddFlower(RandomKind(), e.Location);
            }
        }

        /// <summary>
        /// One animation step: every flower grows and moves (bouncing off the walls of the panel's current size),
        /// then flowers that overlap bounce off each other, then the panel is repainted.
        /// </summary>
        private void animationTimer_Tick(object sender, EventArgs e)
        {
            Rectangle bounds = gardenPanel.ClientRectangle;

            // The panel has no size while the window is minimized; wait until it comes back.
            if (bounds.Width == 0 || bounds.Height == 0)
            {
                return;
            }

            foreach (object item in _flowers)
            {
                // Late binding: the reference is a Flower, but the Move that runs belongs to the flower's real type.
                Flower flower = (Flower)item;
                flower.Move(bounds);
            }

            CollisionDetector.BounceOffEachOther(_flowers);

            gardenPanel.Invalidate();
        }

        private void gardenPanel_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            foreach (object item in _flowers)
            {
                Flower flower = (Flower)item;
                flower.Draw(e.Graphics);
            }
        }

        /// <summary>When the window shrinks, pulls any flower that is now past an edge back inside.</summary>
        private void gardenPanel_Resize(object sender, EventArgs e)
        {
            Rectangle bounds = gardenPanel.ClientRectangle;

            if (bounds.Width == 0 || bounds.Height == 0)
            {
                return;
            }

            foreach (object item in _flowers)
            {
                Flower flower = (Flower)item;
                flower.KeepInside(bounds);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Creates a lily of the given kind with a random size, direction and starting colour, centres it on
        /// <paramref name="center"/> (kept inside the panel) and adds it to the garden.
        /// </summary>
        private void AddFlower(FlowerKind kind, Point center)
        {
            PointF velocity = RandomVelocity();
            float bloomSize = _random.Next(MinBloomSize, MaxBloomSize + 1);
            Flower flower;

            // Each new flower is explicitly upcast from its own class to Flower.
            switch (kind)
            {
                case FlowerKind.SpiderLily:
                    flower = (Flower)new SpiderLily(velocity, RandomSpiderLilyColor(), bloomSize);
                    break;
                case FlowerKind.CallaLily:
                    flower = (Flower)new CallaLily(velocity, bloomSize, _random.Next(CallaLily.PaletteSize));
                    break;
                case FlowerKind.TigerLily:
                    flower = (Flower)new TigerLily(velocity, bloomSize);
                    break;
                case FlowerKind.LilyOfTheValley:
                    flower = (Flower)new LilyOfTheValley(velocity, bloomSize, gardenPanel);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("kind", kind, "Unknown kind of flower.");
            }

            flower.CenterOn(center);
            flower.KeepInside(gardenPanel.ClientRectangle);

            _flowers.Add((object)flower);

            gardenPanel.Invalidate();
            UpdateStatus();
        }

        private FlowerKind RandomKind()
        {
            return (FlowerKind)_random.Next(Enum.GetValues(typeof(FlowerKind)).Length);
        }

        private Color RandomSpiderLilyColor()
        {
            return SpiderLilyColors[_random.Next(SpiderLilyColors.Length)];
        }

        private Point RandomPointInGarden()
        {
            Size size = gardenPanel.ClientSize;

            return new Point(_random.Next(Math.Max(1, size.Width)), _random.Next(Math.Max(1, size.Height)));
        }

        /// <summary>A velocity in a random direction with a speed between <see cref="MinSpeed"/> and <see cref="MaxSpeed"/>.</summary>
        private PointF RandomVelocity()
        {
            double angle = _random.NextDouble() * 2 * Math.PI;
            double speed = MinSpeed + _random.NextDouble() * (MaxSpeed - MinSpeed);

            return new PointF((float)(Math.Cos(angle) * speed), (float)(Math.Sin(angle) * speed));
        }

        /// <summary>
        /// Switches between a normal window and a borderless window covering the whole screen.
        /// The flowers adjust to the new panel size on the next tick.
        /// </summary>
        private void SetFullScreen(bool fullScreen)
        {
            if (fullScreen)
            {
                _windowedBorderStyle = FormBorderStyle;
                _windowedState = WindowState;

                // Back to Normal first so an already-maximized window is re-maximized without its border,
                // covering the taskbar too.
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Normal;
                WindowState = FormWindowState.Maximized;
            }
            else
            {
                FormBorderStyle = _windowedBorderStyle;
                WindowState = _windowedState;
            }

            _isFullScreen = fullScreen;
            fullScreenMenuItem.Checked = fullScreen;
        }

        private void UpdateStatus()
        {
            statusLabel.Text = string.Format(CultureInfo.CurrentCulture, "Lilies: {0}   |   {1}",
                _flowers.Count, animationTimer.Enabled ? RunningHint : PausedHint);
        }

        /// <summary>
        /// Reads a positive whole-number setting from App.config, or returns <paramref name="defaultValue"/>
        /// if it is missing or not a valid positive number.
        /// </summary>
        private static int ReadSetting(string key, int defaultValue)
        {
            int value;

            return int.TryParse(ConfigurationManager.AppSettings[key], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value) && value > 0
                ? value
                : defaultValue;
        }

        #endregion
    }
}
