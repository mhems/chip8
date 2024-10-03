using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace emulator
{
    public class Chip8Presenter
    {
        public const string ShiftIgnoresYOption = "ShiftIgnoresY";
        public const string BitwiseResetsVfOption = "BitwiseResetVF";
        public const string MemoryIncrementsIOption = "MemoryIncrementsI";
        public const string JumpUsesV0Option = "JumpUsesV0";
        private readonly Chip8 model;
        private readonly IChip8View view;

        public Chip8Presenter(Chip8 model, IChip8View view)
        {
            this.model = model;
            this.view = view;

            view.KeyBoardKeyUp += HandleKeyUp;
            view.KeyBoardKeyDown += HandleKeyDown;
            view.ProgramLoaded += HandleProgramLoaded;
            view.ProgramStarted += HandleProgramStarted;
            view.ConfigChanged += HandleConfigChanged;

            model.SoundTimerChanged += HandleSoundChange;
            model.ScreenUpdated += HandleScreenChange;
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
        }

        private void HandleProgramStarted(object? sender, EventArgs e)
        {
            model.Execute();
        }

        private void HandleConfigChanged(object? sender, IChip8View.ConfigChangedEventArgs e)
        {
            switch (e.Option)
            {
                case ShiftIgnoresYOption:
                    model.ShiftIgnoresY = e.Value;
                    break;
                case JumpUsesV0Option:
                    model.JumpUsesV0 = e.Value;
                    break;
                case MemoryIncrementsIOption:
                    model.MemoryIncrementsI = e.Value;
                    break;
                case BitwiseResetsVfOption:
                    model.BitwiseResetFlags = e.Value;
                    break; ;
            }
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
