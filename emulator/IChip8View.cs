using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace emulator
{
    public interface IChip8View
    {
        public event EventHandler<KeyChangedEventArgs> KeyBoardKeyUp;
        public event EventHandler<KeyChangedEventArgs> KeyBoardKeyDown;
        public event EventHandler<ProgramLoadedEventArgs> ProgramLoaded;

        public void UpdateSoundState(bool soundOn);

        public void UpdateScreen(ulong[] screen);

        public void Draw();

        public class KeyChangedEventArgs(byte keyCode) : EventArgs
        {
            public byte KeyCode { get; private set; } = keyCode;
        }

        public class ProgramLoadedEventArgs(string filename) : EventArgs
        {
            public string FileName { get; private set; } = filename;
        }
    }
}
