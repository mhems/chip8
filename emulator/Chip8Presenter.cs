using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace emulator
{
    public class Chip8Presenter
    {
        private readonly Chip8 model;
        private readonly IChip8View view;

        public Chip8Presenter(Chip8 model, IChip8View view)
        {
            this.model = model;
            this.view = view;

            view.KeyBoardKeyUp += HandleKeyUp;
            view.KeyBoardKeyDown += HandleKeyDown;
            view.ProgramLoaded += HandleProgramLoaded;

            model.SoundTimerChanged += HandleSoundChange;
            model.ScreenUpdated += HandleScreenChange;
            model.Ticked += HandleTick;
        }

        private void HandleKeyUp(object? sender, IChip8View.KeyChangedEventArgs e)
        {
            model.SetKey(e.KeyCode, false);
        }

        private void HandleKeyDown(object? sender, IChip8View.KeyChangedEventArgs e)
        {
            model.SetKey(e.KeyCode, true);
        }

        private void HandleProgramLoaded(object? sender, IChip8View.ProgramLoadedEventArgs e)
        {
            model.LoadProgram(e.FileName);
            model.Execute();
        }

        private void HandleTick(object? sender, Chip8.TickEvent e)
        {
            view.Draw();
        }

        private void HandleSoundChange(object? sender, Chip8.SoundEvent e)
        {
            view.UpdateSoundState(e.On);
        }

        private void HandleScreenChange(object? sender, Chip8.ScreenEvent e)
        {
            view.UpdateScreen(e.Screen);
        }
    }
}
