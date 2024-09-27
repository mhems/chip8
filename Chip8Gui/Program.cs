using emulator;

namespace Chip8Gui
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            Chip8Gui gui = new();
            Chip8 cpu = new();
            _ = new Chip8Presenter(cpu, gui);
            Application.Run(gui);
        }
    }
}