using emulator;
using System.ComponentModel.Design.Serialization;
using System.Data;
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
        public event EventHandler<EventArgs>? ProgramStarted;
        public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

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
            this.offColorPanel.BackColor = pixelPanel.OffColor;
            this.onColorPanel.BackColor = pixelPanel.OnColor;
            this.Controls.Add(pixelPanel);
           // this.Width = pixelPanel.Width + 50;
            //this.Height = pixelPanel.Height + 50;

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
            pixelPanel.Clear();
            jumpV0Checkbox.Enabled = false;
            memoryIncrementsCheckbox.Enabled = false;
            bitwiseResetCheckbox.Enabled = false;
            shiftIgnoresYCheckbox.Enabled = false;
            Task.Run(() => ProgramStarted?.Invoke(this, new EventArgs()));
        }

        private void OffButton_Click(object sender, EventArgs e)
        {
            Color color = PromptColor(offColorPanel.BackColor);
            offColorPanel.BackColor = color;
            pixelPanel.OffColor = color;
        }

        private void OnButton_Click(object sender, EventArgs e)
        {
            Color color = PromptColor(onColorPanel.BackColor);
            onColorPanel.BackColor = color;
            pixelPanel.OnColor = color;
        }

        private static Color PromptColor(Color initial)
        {
            ColorDialog dialog = new()
            {
                ShowHelp = true,
                Color = initial,
                AllowFullOpen = false,
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                return dialog.Color;
            }

            return initial;
        }

        private void ShiftIgnoresYCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            ConfigChanged?.Invoke(this,
                new ConfigChangedEventArgs(Chip8Presenter.ShiftIgnoresYOption,
                    shiftIgnoresYCheckbox.Checked));
        }

        private void JumpV0Checkbox_CheckedChanged(object sender, EventArgs e)
        {
            ConfigChanged?.Invoke(this,
                new ConfigChangedEventArgs(Chip8Presenter.JumpUsesV0Option,
                    jumpV0Checkbox.Checked));
        }

        private void BitwiseResetCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            ConfigChanged?.Invoke(this,
                new ConfigChangedEventArgs(Chip8Presenter.BitwiseResetsVfOption,
                    bitwiseResetCheckbox.Checked));
        }

        private void MemoryIncrementsCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            ConfigChanged?.Invoke(this,
                new ConfigChangedEventArgs(Chip8Presenter.MemoryIncrementsIOption,
                    memoryIncrementsCheckbox.Checked));
        }

        private void LoadProgramButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = "Chip8 ROMs (*.ch8)|*.ch8|All Files (*.*)|*.*",
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory + "\\roms",
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string filepath = new FileInfo(dialog.FileName).FullName;
                loadedRomLabel.Text = Path.GetFileNameWithoutExtension(filepath);
                Task.Run(() => ProgramLoaded?.Invoke(this, new ProgramLoadedEventArgs(filepath)));
            }
        }
    }
}
