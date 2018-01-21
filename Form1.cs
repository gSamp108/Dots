using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Dots
{
    public partial class Form1 : Form
    {
        private Engine engine;
        private Dictionary<int, Color> ColorByOwnerId = new Dictionary<int, Color>();
        private Random Rng = new Random();
        private Image canvas;

        public Form1()
        {
            InitializeComponent();
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        private void InitializeEngine()
        {
            if (this.engine != null) this.engine.Stop();
            this.engine = new Engine(4, 20, 20);
            this.engine.Start();
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            //this.InitializeCanvas();
           // this.InitializeEngine();
        }

        private void InitializeCanvas()
        {
            if (this.canvas != null) this.canvas.Dispose();
            this.canvas = new Bitmap(this.ClientRectangle.Width, this.ClientRectangle.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (this.canvas == null) this.InitializeCanvas();
            if (this.engine == null) this.InitializeEngine();

            using (var render = Graphics.FromImage(this.canvas))
            {
                using (var brush = new SolidBrush(Color.Black))
                {
                    var changes = this.engine.GetRenderer();
                    foreach (var change in changes)
                    {
                        brush.Color = this.GetColor(change.Owner);
                        render.FillRectangle(brush, new Rectangle(change.Position.X * 8, change.Position.Y * 8, 8, 8));
                        if (change.Dot > 0)
                        {
                            brush.Color = this.GetColor(change.Dot);
                            render.FillRectangle(brush, new Rectangle((change.Position.X * 8) + 2, (change.Position.Y * 8) + 2, 4, 4));
                            render.DrawRectangle(Pens.Black, new Rectangle((change.Position.X * 8) + 2, (change.Position.Y * 8) + 2, 4, 4));
                        }
                        render.DrawRectangle(Pens.Black, new Rectangle(change.Position.X * 8, change.Position.Y * 8, 8, 8));
                    }
                }
            }
            e.Graphics.DrawImageUnscaled(this.canvas, new Point());
        }

        private Color GetColor(int ownerId)
        {
            if (!this.ColorByOwnerId.ContainsKey(ownerId)) this.ColorByOwnerId.Add(ownerId, Color.FromArgb(this.Rng.Next(256), this.Rng.Next(256), this.Rng.Next(256)));
            return this.ColorByOwnerId[ownerId];
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (this.engine != null) this.engine.Stop();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            this.Invalidate();
        }
    }
}
