namespace prog2200_assign2
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.mainMenuStrip = new System.Windows.Forms.MenuStrip();
            this.gardenMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addDaisyMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addTulipMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addAsciiSproutMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addLabelBouquetMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addRandomMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.pauseMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.clearMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fullScreenMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.gardenPanel = new prog2200_assign2.GardenPanel();
            this.animationTimer = new System.Windows.Forms.Timer(this.components);
            this.mainMenuStrip.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // mainMenuStrip
            //
            this.mainMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.gardenMenuItem,
            this.viewMenuItem});
            this.mainMenuStrip.Location = new System.Drawing.Point(0, 0);
            this.mainMenuStrip.Name = "mainMenuStrip";
            this.mainMenuStrip.Size = new System.Drawing.Size(900, 24);
            this.mainMenuStrip.TabIndex = 0;
            //
            // gardenMenuItem
            //
            this.gardenMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addDaisyMenuItem,
            this.addTulipMenuItem,
            this.addAsciiSproutMenuItem,
            this.addLabelBouquetMenuItem,
            this.addRandomMenuItem,
            this.addSeparator,
            this.pauseMenuItem,
            this.clearMenuItem,
            this.exitSeparator,
            this.exitMenuItem});
            this.gardenMenuItem.Name = "gardenMenuItem";
            this.gardenMenuItem.Size = new System.Drawing.Size(58, 20);
            this.gardenMenuItem.Text = "&Garden";
            //
            // addDaisyMenuItem
            //
            this.addDaisyMenuItem.Name = "addDaisyMenuItem";
            this.addDaisyMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D1)));
            this.addDaisyMenuItem.Size = new System.Drawing.Size(240, 22);
            this.addDaisyMenuItem.Text = "Add &Daisy (gradient)";
            this.addDaisyMenuItem.Click += new System.EventHandler(this.addDaisyMenuItem_Click);
            //
            // addTulipMenuItem
            //
            this.addTulipMenuItem.Name = "addTulipMenuItem";
            this.addTulipMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D2)));
            this.addTulipMenuItem.Size = new System.Drawing.Size(240, 22);
            this.addTulipMenuItem.Text = "Add &Tulip (polygon)";
            this.addTulipMenuItem.Click += new System.EventHandler(this.addTulipMenuItem_Click);
            //
            // addAsciiSproutMenuItem
            //
            this.addAsciiSproutMenuItem.Name = "addAsciiSproutMenuItem";
            this.addAsciiSproutMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D3)));
            this.addAsciiSproutMenuItem.Size = new System.Drawing.Size(240, 22);
            this.addAsciiSproutMenuItem.Text = "Add &ASCII Sprout (text)";
            this.addAsciiSproutMenuItem.Click += new System.EventHandler(this.addAsciiSproutMenuItem_Click);
            //
            // addLabelBouquetMenuItem
            //
            this.addLabelBouquetMenuItem.Name = "addLabelBouquetMenuItem";
            this.addLabelBouquetMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D4)));
            this.addLabelBouquetMenuItem.Size = new System.Drawing.Size(240, 22);
            this.addLabelBouquetMenuItem.Text = "Add &Label Bouquet (control)";
            this.addLabelBouquetMenuItem.Click += new System.EventHandler(this.addLabelBouquetMenuItem_Click);
            //
            // addRandomMenuItem
            //
            this.addRandomMenuItem.Name = "addRandomMenuItem";
            this.addRandomMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R)));
            this.addRandomMenuItem.Size = new System.Drawing.Size(240, 22);
            this.addRandomMenuItem.Text = "Add &Random Flower";
            this.addRandomMenuItem.Click += new System.EventHandler(this.addRandomMenuItem_Click);
            //
            // addSeparator
            //
            this.addSeparator.Name = "addSeparator";
            this.addSeparator.Size = new System.Drawing.Size(237, 6);
            //
            // pauseMenuItem
            //
            this.pauseMenuItem.Name = "pauseMenuItem";
            this.pauseMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.P)));
            this.pauseMenuItem.Size = new System.Drawing.Size(240, 22);
            this.pauseMenuItem.Text = "&Pause";
            this.pauseMenuItem.Click += new System.EventHandler(this.pauseMenuItem_Click);
            //
            // clearMenuItem
            //
            this.clearMenuItem.Name = "clearMenuItem";
            this.clearMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Delete)));
            this.clearMenuItem.Size = new System.Drawing.Size(240, 22);
            this.clearMenuItem.Text = "&Clear Garden";
            this.clearMenuItem.Click += new System.EventHandler(this.clearMenuItem_Click);
            //
            // exitSeparator
            //
            this.exitSeparator.Name = "exitSeparator";
            this.exitSeparator.Size = new System.Drawing.Size(237, 6);
            //
            // exitMenuItem
            //
            this.exitMenuItem.Name = "exitMenuItem";
            this.exitMenuItem.Size = new System.Drawing.Size(240, 22);
            this.exitMenuItem.Text = "E&xit";
            this.exitMenuItem.Click += new System.EventHandler(this.exitMenuItem_Click);
            //
            // viewMenuItem
            //
            this.viewMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fullScreenMenuItem});
            this.viewMenuItem.Name = "viewMenuItem";
            this.viewMenuItem.Size = new System.Drawing.Size(44, 20);
            this.viewMenuItem.Text = "&View";
            //
            // fullScreenMenuItem
            //
            this.fullScreenMenuItem.Name = "fullScreenMenuItem";
            this.fullScreenMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F11;
            this.fullScreenMenuItem.Size = new System.Drawing.Size(180, 22);
            this.fullScreenMenuItem.Text = "&Full Screen";
            this.fullScreenMenuItem.Click += new System.EventHandler(this.fullScreenMenuItem_Click);
            //
            // statusStrip
            //
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 578);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(900, 22);
            this.statusStrip.TabIndex = 2;
            //
            // statusLabel
            //
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(0, 17);
            //
            // gardenPanel
            //
            this.gardenPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(32)))), ((int)(((byte)(24)))));
            this.gardenPanel.Cursor = System.Windows.Forms.Cursors.Hand;
            this.gardenPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gardenPanel.Location = new System.Drawing.Point(0, 24);
            this.gardenPanel.Name = "gardenPanel";
            this.gardenPanel.Size = new System.Drawing.Size(900, 554);
            this.gardenPanel.TabIndex = 1;
            this.gardenPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.gardenPanel_Paint);
            this.gardenPanel.MouseClick += new System.Windows.Forms.MouseEventHandler(this.gardenPanel_MouseClick);
            this.gardenPanel.Resize += new System.EventHandler(this.gardenPanel_Resize);
            //
            // animationTimer
            //
            this.animationTimer.Interval = 30;
            this.animationTimer.Tick += new System.EventHandler(this.animationTimer_Tick);
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 600);
            this.Controls.Add(this.gardenPanel);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.mainMenuStrip);
            this.MainMenuStrip = this.mainMenuStrip;
            this.MinimumSize = new System.Drawing.Size(320, 240);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ASCII Garden Screen Saver";
            this.mainMenuStrip.ResumeLayout(false);
            this.mainMenuStrip.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip mainMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem gardenMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addDaisyMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addTulipMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addAsciiSproutMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addLabelBouquetMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addRandomMenuItem;
        private System.Windows.Forms.ToolStripSeparator addSeparator;
        private System.Windows.Forms.ToolStripMenuItem pauseMenuItem;
        private System.Windows.Forms.ToolStripMenuItem clearMenuItem;
        private System.Windows.Forms.ToolStripSeparator exitSeparator;
        private System.Windows.Forms.ToolStripMenuItem exitMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fullScreenMenuItem;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private GardenPanel gardenPanel;
        private System.Windows.Forms.Timer animationTimer;
    }
}
