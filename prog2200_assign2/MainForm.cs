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
    /// The ASCII Lily Garden screen saver window: one vase at the bottom middle of the garden, with every lily's stem
    /// branching out of its mouth. All lily shapes live in prog2200_assign2_library; this form keeps them in an
    /// ArrayList and processes them polymorphically: on every timer tick each item is cast back to
    /// <see cref="Flower"/> and told to Move, and on every repaint it is told to Draw. Which Move and Draw actually
    /// run is decided at run time by the flower's real type (late binding).
    /// </summary>
    public partial class MainForm : Form
    {
        // Used when App.config does not set a value.
        private const int DefaultTickIntervalMs = 30;
        private const int DefaultStartingFlowersPerKind = 1;
        private const int DefaultMaxFlowers = 16;

        // Full-bloom width range for the lily heads, in pixels.
        private const int MinBloomSize = 90;
        private const int MaxBloomSize = 130;

        // Drifting speed range, in pixels per tick.
        private const double MinSpeed = 0.4;
        private const double MaxSpeed = 0.9;

        // How far a new stem's direction can stray either side of where it was aimed, or of straight up, in radians.
        private const double AimSpread = Math.PI / 6;
        private const double UpwardSpread = Math.PI / 4;

        // A right-click puts one of each kind in, fully grown, spread this many pixels around the click.
        private const float BouquetSpread = 70f;

        // Lilies wilt one at a time, at least this many milliseconds apart.
        private const int WiltGapMs = 1500;

        // Pixels kept clear between the furthest a head can reach and the garden's edge, and the shortest full reach
        // a stem can have in a very small garden.
        private const float ReachMargin = 20f;
        private const float MinReach = 50f;

        // When the garden is made bigger, one new seedling is planted for every this many new square pixels.
        private const int AreaPerNewSeedling = 150000;

        private const string RunningHint = "Click: add a grown lily   Right-click: add one of each";
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

        // The one vase every stem grows from.
        private readonly Vase _vase = new Vase();

        // Lets lilies that are ready to wilt go one at a time, on its own background thread.
        private readonly WiltScheduler _wiltScheduler = new WiltScheduler(WiltGapMs);

        // Read from the appSettings section of App.config.
        private readonly int _startingFlowersPerKind = ReadSetting("StartingFlowersPerKind", DefaultStartingFlowersPerKind);

        // Lilies that sprout by themselves (from pollination or a bigger window) stop once the garden holds this many.
        private readonly int _maxFlowers = ReadSetting("MaxFlowers", DefaultMaxFlowers);

        // Garden area at the last resize, and new area added since the last seedling was planted for it.
        private int _lastGardenArea;
        private int _grownGardenArea;

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
        /// Places the vase, plants the first seeds in it (they branch out from there) and starts the animation.
        /// Done here rather than in the constructor because the panel only has its real size once the window is on screen.
        /// </summary>
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
        /// A left-click puts a fully grown random lily into the vase with its head at the click; a right-click puts
        /// in one of every kind, spread around the click. They carry on from full bloom to wilting, then resprout.
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
        /// One animation step: every flower grows and moves along its stem (bouncing off the walls of the panel's
        /// current size), then flowers that are about to touch bounce off each other, then lilies that are ready to
        /// wilt are queued and the one whose turn has come starts wilting, then the panel is repainted. Two fresh
        /// blooms that meet pollinate each other, and a new lily grows from the vase towards where they met.
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

        /// <summary>Draws the stems first, then the vase over their bottoms, then the lily heads.</summary>
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
        /// Moves the vase to the new bottom middle and reties every stem to it with a reach that fits the new size,
        /// and pulls any flower that is now past an edge back inside. When the garden grows, new lilies sprout:
        /// one for every <see cref="AreaPerNewSeedling"/> new square pixels.
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

            // Nothing is planted for the resizes that happen while the window is first laid out.
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

        /// <summary>
        /// Hands every lily that is ready to wilt to the wilt scheduler, then starts wilting the one whose turn it is
        /// (if it is still in the garden). The scheduler lets them go one at a time, so they never all wilt together.
        /// </summary>
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

        /// <summary>
        /// Plants a seed of the given kind in the vase mouth, with its stem heading towards <paramref name="aim"/>.
        /// </summary>
        private void AddFlower(FlowerKind kind, Point aim)
        {
            Flower flower = CreateFlower(kind, aim);

            flower.CenterOn(_vase.Mouth);
            AddToGarden(flower);
        }

        /// <summary>
        /// Puts a fully grown lily of the given kind into the vase with its head at <paramref name="head"/>.
        /// It is in full bloom straight away and carries on from there: wilting, then resprouting from a seed.
        /// </summary>
        private void AddGrownFlower(FlowerKind kind, Point head)
        {
            Flower flower = CreateFlower(kind, head);

            flower.GrowFully();
            flower.CenterOn(head);
            flower.KeepInside(gardenPanel.ClientRectangle);
            AddToGarden(flower);
        }

        /// <summary>Puts one fully grown lily of every kind into the vase, with their heads spread around <paramref name="centre"/>.</summary>
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

        /// <summary>
        /// Creates a lily of the given kind with a random size and starting colour, heading towards
        /// <paramref name="aim"/>, with its stem tied to the vase mouth.
        /// </summary>
        private Flower CreateFlower(FlowerKind kind, Point aim)
        {
            PointF velocity = VelocityTowards(aim);
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

            flower.TieTo(_vase.Mouth, MaxReach());

            return flower;
        }

        private void AddToGarden(Flower flower)
        {
            _flowers.Add((object)flower);

            gardenPanel.Invalidate();
            UpdateStatus();
        }

        /// <summary>Plants one seed of every kind in the vase, all heading roughly towards <paramref name="aim"/>.</summary>
        private void ScatterSeeds(Point aim)
        {
            foreach (FlowerKind kind in Enum.GetValues(typeof(FlowerKind)))
            {
                AddFlower(kind, aim);
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

        /// <summary>A random point somewhere above the vase mouth, for a new lily to head towards.</summary>
        private Point RandomPointAboveVase()
        {
            Size size = gardenPanel.ClientSize;
            int mouthHeight = Math.Max(1, (int)_vase.Mouth.Y);

            return new Point(_random.Next(Math.Max(1, size.Width)), _random.Next(mouthHeight));
        }

        /// <summary>
        /// The longest a stem can reach in the current garden: from the vase mouth to a top corner, so a fully grown
        /// lily can reach the top of the garden anywhere across its width. It changes whenever the window is resized.
        /// </summary>
        private float MaxReach()
        {
            Size size = gardenPanel.ClientSize;
            float toTop = _vase.Mouth.Y;
            float toSide = size.Width / 2f;

            return Math.Max(MinReach, (float)Math.Sqrt(toTop * toTop + toSide * toSide) - ReachMargin);
        }

        /// <summary>
        /// A velocity heading from the vase mouth towards <paramref name="aim"/> (give or take a little), with a speed
        /// between <see cref="MinSpeed"/> and <see cref="MaxSpeed"/>. If the aim is not above the vase the lily heads
        /// roughly upwards instead, so the bouquet stands up.
        /// </summary>
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
