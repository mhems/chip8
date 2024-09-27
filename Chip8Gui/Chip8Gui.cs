using emulator;
using System.Diagnostics;
using System.Media;

namespace Chip8Gui
{
    public partial class Chip8Gui : Form, IChip8View
    {
        public event EventHandler<IChip8View.KeyChangedEventArgs>? KeyBoardKeyUp;
        public event EventHandler<IChip8View.KeyChangedEventArgs>? KeyBoardKeyDown;
        public event EventHandler<IChip8View.ProgramLoadedEventArgs>? ProgramLoaded;

        private const int PIXEL_SIZE = 20;
        private readonly Keys[] keyboard = [
            Keys.Q, Keys.W, Keys.E, Keys.R,
            Keys.A, Keys.S, Keys.D, Keys.F,
            Keys.Z, Keys.X, Keys.C, Keys.V];
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
            t.Start();
        }

        public void UpdateScreen(ulong[] screen)
        {
            pixelPanel.UpdatePixels(screen);
        }

        public void UpdateSoundState(bool soundOn)
        {
            playSound = soundOn;
        }

        public void HandleKeyUp(object sender, KeyEventArgs e)
        {
            for (int i = 0; i < keyboard.Length; i++)
            {
                if (keyboard[i] == e.KeyCode)
                {
                    Task.Run(() => KeyBoardKeyUp?.Invoke(this, new IChip8View.KeyChangedEventArgs((byte)i)));
                    break;
                }
            }
        }

        public void HandleKeyDown(object sender, KeyEventArgs e)
        {
            for (int i = 0; i < keyboard.Length; i++)
            {
                if (keyboard[i] == e.KeyCode)
                {
                    Task.Run(() => KeyBoardKeyDown?.Invoke(this, new IChip8View.KeyChangedEventArgs((byte)i)));
                    break;
                }
            }
        }

        private void SoundLoop()
        {
            while (true)
            {
                if (playSound)
                {
                    SystemSounds.Beep.Play();
                }

                Thread.Sleep(3000);
            }
        }

        private void ExecuteButton_Click(object sender, EventArgs e)
        {
            string s = "4-flags.ch8";
            ProgramLoaded?.Invoke(this, new IChip8View.ProgramLoadedEventArgs(s));
        }
    }
}
