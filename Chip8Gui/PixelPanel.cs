using System;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.Drawing.Drawing2D;

namespace Chip8Gui
{
    public partial class PixelPanel : Panel
    {
        private readonly int rowCount;
        private readonly int colCount;
        private readonly Pixel[,] pixels;

        public PixelPanel(int pixelSize)
        {
            this.DoubleBuffered = true;
            this.ResizeRedraw = false;
 
            this.rowCount = 32;
            this.colCount = 64;
            pixels = new Pixel[rowCount, colCount];
            for (int r = 0; r < rowCount; r++)
            {
                for (int c = 0; c < colCount; c++)
                {
                    pixels[r, c] = new Pixel()
                    {
                        Bounds = new Rectangle(c * pixelSize, r * pixelSize, pixelSize, pixelSize),
                        On = false
                    };
                }
            }
        }

        public void UpdatePixels(ulong[] pixels)
        {
            for (int r = 0; r < rowCount; r++)
            {
                for (int c = 0; c < colCount; c++)
                {
                    this.pixels[r, c].On = (pixels[r] & (1ul << (colCount - c - 1))) != 0;
                }
            }

            Redraw();
        }

        public void Redraw()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(this.Refresh));
            }
            else
            {
                this.Refresh();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Color green3 = Color.FromArgb(255, 51, 255, 51);
            SolidBrush greenBrush = new(green3);
            for (int r = 0; r < rowCount; r++)
            {
                for (int c = 0; c < colCount; c++)
                {
                    Brush brush = pixels[r, c].On ? greenBrush : Brushes.Black;
                    e.Graphics.FillRectangle(brush, pixels[r, c].Bounds);
                    e.Graphics.DrawRectangle(new Pen(brush), pixels[r, c].Bounds);
                }
            }

            base.OnPaint(e);
        }
    }

    internal struct Pixel
    {
        public Rectangle Bounds { get; set; }
        public bool On { get; set; }
    }
}
