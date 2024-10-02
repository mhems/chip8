using emulator;
using System.ComponentModel.Design.Serialization;
using System.Diagnostics;
using System.Media;
using static emulator.IChip8View;

namespace Chip8Gui
{
    public partial class Chip8Gui : Form, IChip8View
    {
        public event EventHandler<KeyChangedEventArgs>? KeyBoardKeyUp;
        public event EventHandler<KeyChangedEventArgs>? KeyBoardKeyDown;
        public event EventHandler<ProgramLoadedEventArgs>? ProgramLoaded;

        private const int PIXEL_SIZE = 20;
        private readonly Dictionary<Keys, byte> keyMap = new() {
            {Keys.D1, 0x1},
            {Keys.D2, 0x2},
            {Keys.D3, 0x3},
            {Keys.D4, 0xC},
            {Keys.Q,  0x4},
            {Keys.W,  0x5},
            {Keys.E,  0x6},
            {Keys.R,  0xD},
            {Keys.A,  0x7},
            {Keys.S,  0x8},
            {Keys.D,  0x9},
            {Keys.F,  0xE},
            {Keys.Z,  0xA},
            {Keys.X,  0x0},
            {Keys.C,  0xB},
            {Keys.V,  0xF}
        };
        private readonly PixelPanel pixelPanel;

        private bool playSound;

        public Chip8Gui()
        {
            InitializeComponent();
            pixelPanel = new(PIXEL_SIZE)
            {
                Height = 32 * PIXEL_SIZE + 50,
                Width = 64 * PIXEL_SIZE + 50,
            };
            this.Controls.Add(pixelPanel);
            this.Width = pixelPanel.Width + 50;
            this.Height = pixelPanel.Height + 50;

            Thread t = new(SoundLoop);
            //t.Start();
        }

        public void UpdateScreen(ulong[] screen)
        {
            pixelPanel.UpdatePixels(screen);
        }

        public void UpdateSoundState(bool soundOn)
        {
            playSound = soundOn;
        }

        public void Draw()
        {
        }

        public void HandleKeyUp(object sender, KeyEventArgs e)
        {
            HandleKeyToggle(e.KeyCode, KeyBoardKeyUp);
        }

        public void HandleKeyDown(object sender, KeyEventArgs e)
        {
            HandleKeyToggle(e.KeyCode, KeyBoardKeyDown);
        }

        private void HandleKeyToggle(Keys keyCode, EventHandler<KeyChangedEventArgs>? toInvoke)
        {
            if (keyMap.TryGetValue(keyCode, out byte value))
            {
                Task.Run(() => toInvoke?.Invoke(this, new KeyChangedEventArgs(value)));
            }
        }

        private void SoundLoop()
        {
            while (true)
            {
                if (playSound)
                {
                   // SystemSounds.Beep.Play();
                }

                Thread.Sleep(10);
            }
        }

        private void ExecuteButton_Click(object sender, EventArgs e)
        {
            string s = "roms\\3-corax+.ch8";
            Task.Run(() => ProgramLoaded?.Invoke(this, new IChip8View.ProgramLoadedEventArgs(s)));
        }
    }
}
