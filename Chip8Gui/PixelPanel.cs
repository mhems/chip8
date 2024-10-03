using System;
using System.Configuration;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace Chip8Gui
{
    public partial class PixelPanel : Panel
    {
        private const string OffColorKey = "OffColor";
        private const string OnColorKey = "OnColor";
        private readonly int rowCount;
        private readonly int colCount;
        private readonly Pixel[,] pixels;
        private Color offColor = Color.Black;
        private Color onColor = Color.White;
        private Brush offBrush;
        private Brush onBrush;
        private readonly Configuration config;
        private readonly KeyValueConfigurationCollection settings;

        public Color OnColor
        {
            get => onColor;
            set
            {
                onBrush = SetColor(value, true);
                onColor = value;
            }
        }

        public Color OffColor
        {
            get => offColor;
            set
            {
                offBrush = SetColor(value, false);
                offColor = value;
            }
        }

        public PixelPanel(int pixelSize)
        {
            this.DoubleBuffered = true;
            this.ResizeRedraw = false;
 
            this.rowCount = 32;
            this.colCount = 64;
            pixels = new Pixel[rowCount, colCount];

            config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            settings = config.AppSettings.Settings;

            offBrush = new SolidBrush(offColor);
            onBrush = new SolidBrush(onColor);
            if (settings[OffColorKey] != null)
            {
                InitColor(false);
            }
            if (settings[OnColorKey] != null)
            {
                InitColor(true);
            }

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

            if (InvokeRequired)
            {
                Invoke(new Action(Refresh));
            }
            else
            {
                Refresh();
            }
        }

        public void Clear()
        {
            UpdatePixels(new ulong[rowCount]);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            for (int r = 0; r < rowCount; r++)
            {
                for (int c = 0; c < colCount; c++)
                {
                    Brush brush = pixels[r, c].On ? onBrush : offBrush;
                    e.Graphics.FillRectangle(brush, pixels[r, c].Bounds);
                    e.Graphics.DrawRectangle(new Pen(brush), pixels[r, c].Bounds);
                }
            }

            base.OnPaint(e);
        }

        private void InitColor(bool on)
        {
            string key = on ? OnColorKey : OffColorKey;
            string value = settings[key].Value;
            if (int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int argb))
            {
                Color color = Color.FromArgb(argb);
                if (on)
                {
                    OnColor = color;
                }
                else
                {
                    OffColor = color;
                }
            }
        }

        private Brush SetColor(Color color, bool on)
        {
            Brush brush = new SolidBrush(color);
            string key = on ? OnColorKey : OffColorKey;
            if (settings[key] == null)
            {
                settings.Add(key, color.ToArgb().ToString("x"));
            }
            else
            {
                settings[key].Value = color.ToArgb().ToString("x");
            }
            config.Save(ConfigurationSaveMode.Modified);
            return brush;
        }
    }

    internal struct Pixel
    {
        public Rectangle Bounds { get; set; }
        public bool On { get; set; }
    }
}
