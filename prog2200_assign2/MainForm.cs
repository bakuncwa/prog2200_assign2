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
    /// The ASCII Lily Garden screen saver window: one vase at the bottom middle, with every lily's stem growing out
    /// of its mouth. The lily shapes are in prog2200_assign2_library.
    /// </summary>
    public partial class MainForm : Form
    {
        // Defaults for when App.config does not set a value.
        private const int DefaultTickIntervalMs = 30;
        private const int DefaultStartingFlowersPerKind = 1;
        private const int DefaultMaxFlowers = 16;

        private const int MinBloomSize = 90;
        private const int MaxBloomSize = 130;

        private const double MinSpeed = 0.4;
        private const double MaxSpeed = 0.9;

        private const double AimSpread = Math.PI / 6;
        private const double UpwardSpread = Math.PI / 4;

        private const float BouquetSpread = 70f;

        private const int WiltGapMs = 1500;

        private const float ReachMargin = 20f;
        private const float MinReach = 50f;

        private const int AreaPerNewSeedling = 150000;

        private const string RunningHint = "Click: add a grown lily   Right-click: add one of each";
        private const string PausedHint = "Paused";

        private static readonly Color[] SpiderLilyColors =
        {
            Color.Crimson,
            Color.OrangeRed,
            Color.HotPink,
            Color.Gold,
            Color.White,
            Color.LightCoral
        };

        // Every flower, upcast explicitly to Flower and object when added and cast
        // explicitly back to Flower whenever it is read.
        private readonly ArrayList _flowers = new ArrayList();

        private readonly Random _random = new Random();

        private readonly Vase _vase = new Vase();

        // Releases lilies that are ready to wilt one at a time, on a background thread.
        private readonly WiltScheduler _wiltScheduler = new WiltScheduler(WiltGapMs);

        private readonly int _startingFlowersPerKind = ReadSetting("StartingFlowersPerKind", DefaultStartingFlowersPerKind);

        // Lilies added automatically (pollination, a bigger window) stop at this many.
        private readonly int _maxFlowers = ReadSetting("MaxFlowers", DefaultMaxFlowers);

        private int _lastGardenArea;
        private int _grownGardenArea;

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

        /// <summary>Plants the first seeds once the panel has its real size, and starts the timer.</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            _vase.Place(gardenPanel.ClientRectangle);

            for (int i = 0; i < _startingFlowersPerKind; i++)
            {
                ScatterSeeds(GardenCentre());
            }

            _lastGardenArea = GardenArea();

            animationTimer.Start();
            UpdateStatus();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            animationTimer.Stop();
            _wiltScheduler.Dispose();

            base.OnFormClosing(e);
        }

        /// <summary>Esc leaves full screen.</summary>
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
            AddFlower(FlowerKind.SpiderLily, RandomPointAboveVase());
        }

        private void addCallaLilyMenuItem_Click(object sender, EventArgs e)
        {
            AddFlower(FlowerKind.CallaLily, RandomPointAboveVase());
        }

        private void addTigerLilyMenuItem_Click(object sender, EventArgs e)
        {
            AddFlower(FlowerKind.TigerLily, RandomPointAboveVase());
        }

        private void addLilyOfTheValleyMenuItem_Click(object sender, EventArgs e)
        {
            AddFlower(FlowerKind.LilyOfTheValley, RandomPointAboveVase());
        }

        private void addRandomMenuItem_Click(object sender, EventArgs e)
        {
            AddFlower(RandomKind(), RandomPointAboveVase());
        }

        private void scatterSeedsMenuItem_Click(object sender, EventArgs e)
        {
            ScatterSeeds(GardenCentre());
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

        /// <summary>
        /// A left-click adds a fully grown random lily with its head at the click; a right-click
        /// adds one of each kind around it.
        /// </summary>
        private void gardenPanel_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                AddGrownFlower(RandomKind(), e.Location);
            }
            else if (e.Button == MouseButtons.Right)
            {
                AddGrownBouquet(e.Location);
            }
        }

        /// <summary>
        /// One animation step: move every flower, bounce them off each other, add a lily where two blooms met,
        /// wilt the next lily in turn, then repaint.
        /// </summary>
        private void animationTimer_Tick(object sender, EventArgs e)
        {
            Rectangle bounds = gardenPanel.ClientRectangle;

            // The panel has no size while the window is minimized.
            if (bounds.Width == 0 || bounds.Height == 0)
            {
                return;
            }

            foreach (object item in _flowers)
            {
                // Late binding, the Move that runs is the flower's own override.
                Flower flower = (Flower)item;
                flower.Move(bounds);
            }

            ArrayList pollinationPoints = CollisionDetector.BounceOffEachOther(_flowers);

            foreach (object item in pollinationPoints)
            {
                if (_flowers.Count >= _maxFlowers)
                {
                    break;
                }

                AddFlower(RandomKind(), Point.Round((PointF)item));
            }

            WiltInTurn();

            gardenPanel.Invalidate();
        }

        /// <summary>Draws the stems, then the vase over their bottoms, then the heads (each with its own Draw).</summary>
        private void gardenPanel_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            foreach (object item in _flowers)
            {
                Flower flower = (Flower)item;
                Stems.Draw(e.Graphics, _vase.Mouth, flower);
            }

            _vase.Draw(e.Graphics);

            foreach (object item in _flowers)
            {
                Flower flower = (Flower)item;
                flower.Draw(e.Graphics);
            }
        }

        /// <summary>
        /// Moves the vase, gives every stem a reach that fits the new size and pulls flowers
        /// back inside. A bigger garden also gets a new lily for every <see cref="AreaPerNewSeedling"/> square pixels.
        /// </summary>
        private void gardenPanel_Resize(object sender, EventArgs e)
        {
            Rectangle bounds = gardenPanel.ClientRectangle;

            if (bounds.Width == 0 || bounds.Height == 0)
            {
                return;
            }

            _vase.Place(bounds);

            foreach (object item in _flowers)
            {
                Flower flower = (Flower)item;
                flower.TieTo(_vase.Mouth, MaxReach());
                flower.KeepInside(bounds);
            }

            // Skip the resizes that happen while the window is first laid out.
            if (_lastGardenArea == 0)
            {
                return;
            }

            int area = GardenArea();

            if (area > _lastGardenArea)
            {
                _grownGardenArea += area - _lastGardenArea;

                while (_grownGardenArea >= AreaPerNewSeedling && _flowers.Count < _maxFlowers)
                {
                    _grownGardenArea -= AreaPerNewSeedling;
                    AddFlower(RandomKind(), RandomPointAboveVase());
                }
            }

            _lastGardenArea = area;
        }

        #endregion

        #region Helpers

        /// <summary>Queues lilies that are ready to wilt, and wilts the one whose turn it is.</summary>
        private void WiltInTurn()
        {
            foreach (object item in _flowers)
            {
                Flower flower = (Flower)item;

                if (flower.IsReadyToWilt)
                {
                    _wiltScheduler.Request(flower);
                }
            }

            Flower next;

            if (_wiltScheduler.TryTakeNext(out next) && _flowers.Contains(next))
            {
                next.Wilt();
            }
        }

        /// <summary>Plants a seed in the vase mouth, its stem heading towards aim.</summary>
        private void AddFlower(FlowerKind kind, Point aim)
        {
            Flower flower = CreateFlower(kind, aim);

            flower.CenterOn(_vase.Mouth);
            AddToGarden(flower);
        }

        /// <summary>Adds a lily already in full bloom with its head at the given point.</summary>
        private void AddGrownFlower(FlowerKind kind, Point head)
        {
            Flower flower = CreateFlower(kind, head);

            flower.GrowFully();
            flower.CenterOn(head);
            flower.KeepInside(gardenPanel.ClientRectangle);
            AddToGarden(flower);
        }

        /// <summary>Adds one fully grown lily of each kind, spread around a point.</summary>
        private void AddGrownBouquet(Point centre)
        {
            Array kinds = Enum.GetValues(typeof(FlowerKind));

            for (int i = 0; i < kinds.Length; i++)
            {
                double angle = i * 2 * Math.PI / kinds.Length;
                Point head = Point.Round(new PointF(centre.X + (float)Math.Cos(angle) * BouquetSpread,
                    centre.Y + (float)Math.Sin(angle) * BouquetSpread));

                AddGrownFlower((FlowerKind)kinds.GetValue(i), head);
            }
        }

        /// <summary>Creates a lily of the given kind with a random size and colour, tied to the vase mouth.</summary>
        private Flower CreateFlower(FlowerKind kind, Point aim)
        {
            PointF velocity = VelocityTowards(aim);
            float bloomSize = _random.Next(MinBloomSize, MaxBloomSize + 1);
            Flower flower;

            // Each new lily is explicitly upcast to Flower.
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

            flower.TieTo(_vase.Mouth, MaxReach());

            return flower;
        }

        private void AddToGarden(Flower flower)
        {
            _flowers.Add((object)flower);

            gardenPanel.Invalidate();
            UpdateStatus();
        }

        /// <summary>Plants one seed of each kind, all heading roughly towards aim.</summary>
        private void ScatterSeeds(Point aim)
        {
            // Enum.GetValues returns a plain Array, so each value is cast explicitly.
            foreach (object kind in Enum.GetValues(typeof(FlowerKind)))
            {
                AddFlower((FlowerKind)kind, aim);
            }
        }

        private FlowerKind RandomKind()
        {
            return (FlowerKind)_random.Next(Enum.GetValues(typeof(FlowerKind)).Length);
        }

        private Color RandomSpiderLilyColor()
        {
            return SpiderLilyColors[_random.Next(SpiderLilyColors.Length)];
        }

        private Point GardenCentre()
        {
            Size size = gardenPanel.ClientSize;

            return new Point(size.Width / 2, size.Height / 2);
        }

        private int GardenArea()
        {
            Size size = gardenPanel.ClientSize;

            return size.Width * size.Height;
        }

        /// <summary>A random point above the vase mouth.</summary>
        private Point RandomPointAboveVase()
        {
            Size size = gardenPanel.ClientSize;
            int mouthHeight = Math.Max(1, (int)_vase.Mouth.Y);

            return new Point(_random.Next(Math.Max(1, size.Width)), _random.Next(mouthHeight));
        }

        /// <summary>A stem's full reach: from the vase mouth to a top corner of the current garden.</summary>
        private float MaxReach()
        {
            Size size = gardenPanel.ClientSize;
            float toTop = _vase.Mouth.Y;
            float toSide = size.Width / 2f;

            return Math.Max(MinReach, (float)Math.Sqrt(toTop * toTop + toSide * toSide) - ReachMargin);
        }

        /// <summary>A velocity from the vase mouth towards aim, or roughly upwards if aim is not above the vase.</summary>
        private PointF VelocityTowards(Point aim)
        {
            PointF mouth = _vase.Mouth;
            double angle;

            if (aim.Y < mouth.Y - ReachMargin)
            {
                angle = Math.Atan2(aim.Y - mouth.Y, aim.X - mouth.X) + (_random.NextDouble() * 2 - 1) * AimSpread;
            }
            else
            {
                angle = -Math.PI / 2 + (_random.NextDouble() * 2 - 1) * UpwardSpread;
            }

            double speed = MinSpeed + _random.NextDouble() * (MaxSpeed - MinSpeed);

            return new PointF((float)(Math.Cos(angle) * speed), (float)(Math.Sin(angle) * speed));
        }

        /// <summary>Switches between a normal window and a borderless full-screen window.</summary>
        private void SetFullScreen(bool fullScreen)
        {
            if (fullScreen)
            {
                _windowedBorderStyle = FormBorderStyle;
                _windowedState = WindowState;

                // Normal first, so an already maximized window is maximized again without its border.
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

        /// <summary>A positive whole-number setting from App.config, or the default if it is missing or invalid.</summary>
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
