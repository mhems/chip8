﻿using System;
using System.Timers;
using System.Diagnostics;
using System.Xml.Schema;

namespace emulator
{
    public class Chip8
    {
        public const uint MEMORY_SIZE_BYTES = 4096;
        public const ushort PROGRAM_START_ADDRESS = 0x200;
        public const ushort FONT_BASE_ADDRESS = 0x0;
        public const byte SCREEN_WIDTH = 64;
        public const byte SCREEN_HEIGHT = 32;
        public const byte TIMER_FREQUENCY_HZ = 60;

        private readonly System.Timers.Timer timer;
        private readonly byte[] registers = new byte[16];
        private readonly Stack<ushort> programStack = new();
        private readonly bool[] keys = new bool[16];
        private byte? mostRecentKeyDown = null;
        private ushort I;
        private readonly byte[] memory;
        private readonly ulong[] vram;
        private byte delayTimer, soundTimer;
        private ushort programCounter;
        private readonly Random random = new();
        private bool running = false;

        private readonly byte[][] sprites =
        [
            [0xF0, 0x90, 0x90, 0x90, 0xF0],
            [0x20, 0x60, 0x20, 0x20, 0x70],
            [0xF0, 0x10, 0xF0, 0x80, 0xF0],
            [0xF0, 0x10, 0xF0, 0x10, 0xF0],
            [0x90, 0x90, 0xF0, 0x10, 0x10],
            [0xF0, 0x80, 0xF0, 0x10, 0xF0],
            [0xF0, 0x80, 0xF0, 0x90, 0xF0],
            [0xF0, 0x10, 0x20, 0x40, 0x40],
            [0xF0, 0x90, 0xF0, 0x90, 0xF0],
            [0xF0, 0x90, 0xF0, 0x10, 0xF0],
            [0xF0, 0x90, 0xF0, 0x90, 0x90],
            [0xE0, 0x90, 0xE0, 0x90, 0xE0],
            [0xF0, 0x80, 0x80, 0x80, 0xF0],
            [0xE0, 0x90, 0x90, 0x90, 0xE0],
            [0xF0, 0x80, 0xF0, 0x80, 0xF0],
            [0xF0, 0x80, 0xF0, 0x80, 0x80]
        ];

        private readonly Dictionary<string, Action<ushort[]>> functionMap = [];

        private ushort CurrentInstruction => (ushort)((memory[programCounter] << 8) | memory[programCounter + 1]);

        public event EventHandler<SoundEvent>? SoundTimerChanged;
        public event EventHandler<ScreenEvent>? ScreenUpdated;

        public bool BitwiseResetFlags { get; set; } = true;
        public bool ShiftIgnoresY { get; set; } = false;
        public bool JumpUsesV0 { get; set; } = true;
        public bool MemoryIncrementsI { get; set; } = true;

        public Chip8()
        {
            memory = new byte[MEMORY_SIZE_BYTES];
            vram = new ulong[SCREEN_HEIGHT];
            timer = new(1000 / TIMER_FREQUENCY_HZ)
            {
                AutoReset = true,
            };
            timer.Elapsed += TimerTick;

            InitializeFunctionMap();
            PutFontsInMemory();
        }

        private void InitializeFunctionMap()
        {
            functionMap["00E0"] = ClearScreen;
            functionMap["00EE"] = Return;
            functionMap["1NNN"] = JumpImmediate;
            functionMap["2NNN"] = Call;
            functionMap["3XNN"] = SkipIfEqualImmediate;
            functionMap["4XNN"] = SkipIfNotEqualImmediate;
            functionMap["5XY0"] = SkipIfEqual;
            functionMap["6XNN"] = SetImmediate;
            functionMap["7XNN"] = AddImmediate;
            functionMap["8XY0"] = Set;
            functionMap["8XY1"] = Or;
            functionMap["8XY2"] = And;
            functionMap["8XY3"] = Xor;
            functionMap["8XY4"] = Add;
            functionMap["8XY5"] = Subtract;
            functionMap["8XY6"] = RightShift;
            functionMap["8XY7"] = ReverseSubtract;
            functionMap["8XYE"] = LeftShift;
            functionMap["9XY0"] = SkipIfNotEqual;
            functionMap["ANNN"] = Store;
            functionMap["BNNN"] = JumpAdditive;
            functionMap["CXNN"] = Random;
            functionMap["DXYN"] = Draw;
            functionMap["EX9E"] = SkipKeyDown;
            functionMap["EXA1"] = SkipKeyUp;
            functionMap["FX07"] = DelayCopy;
            functionMap["FX0A"] = Input;
            functionMap["FX15"] = Delay;
            functionMap["FX18"] = Sound;
            functionMap["FX1E"] = AddMemory;
            functionMap["FX29"] = Sprite;
            functionMap["FX33"] = BinaryCodedDecimal;
            functionMap["FX55"] = Save;
            functionMap["FX65"] = Load;
        }

        private void PutFontsInMemory()
        {
            int spriteSize = sprites[0].Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                for (int b = 0; b < spriteSize; b++)
                {
                    memory[FONT_BASE_ADDRESS + i * spriteSize + b] = sprites[i][b];
                }
            }
        }

        #region instructions

        private void SetImmediate(ushort[] args)
        {
            registers[args[0]] = (byte)(args[1] & 0xFF);
            programCounter += 2;
        }

        private void Set(ushort[] args)
        {
            registers[args[0]] = registers[args[1]];
            programCounter += 2;
        }

        #region arithmetic
        private void AddImmediate(ushort[] args)
        {
            registers[args[0]] += (byte)(args[1] & 0xFF);
            programCounter += 2;
        }

        private void Or(ushort[] args)
        {
            registers[args[0]] |= registers[args[1]];
            if (BitwiseResetFlags)
            {
                registers[0xf] = 0;
            }
            programCounter += 2;
        }

        private void And(ushort[] args)
        {
            registers[args[0]] &= registers[args[1]];
            if (BitwiseResetFlags)
            {
                registers[0xf] = 0;
            }
            programCounter += 2;
        }

        private void Xor(ushort[] args)
        {
            registers[args[0]] ^= registers[args[1]];
            if (BitwiseResetFlags)
            {
                registers[0xf] = 0;
            }
            programCounter += 2;
        }

        private void Add(ushort[] args)
        {
            byte flag = (byte)(registers[args[0]] + registers[args[1]] > 0xff ? 1 : 0);
            registers[args[0]] += registers[args[1]];
            registers[0xf] = flag;
            programCounter += 2;
        }

        private void Subtract(ushort[] args)
        {
            SubtractHelper(args[0], args[1], args[0]);
            programCounter += 2;
        }

        private void ReverseSubtract(ushort[] args)
        {
            SubtractHelper(args[1], args[0], args[0]);
            programCounter += 2;
        }

        private void SubtractHelper(uint a, uint b, uint dest)
        {
            byte flag = (byte)(registers[b] > registers[a] ? 0 : 1);
            registers[dest] = (byte)((registers[a] - registers[b]) & 0xff);
            registers[0xf] = flag;
        }

        private void RightShift(ushort[] args)
        {
            int reg_index = ShiftIgnoresY ? 0 : 1;
            byte flag = (byte)((registers[args[reg_index]] & 0x01) != 0 ? 1 : 0);
            registers[args[0]] = (byte)((registers[args[reg_index]] >> 1) & 0xff);
            registers[0xf] = flag;
            programCounter += 2;
        }

        private void LeftShift(ushort[] args)
        {
            int reg_index = ShiftIgnoresY ? 0 : 1;
            byte flag = (byte)((registers[args[reg_index]] & 0x80) != 0 ? 1 : 0);
            registers[args[0]] = (byte)((registers[args[reg_index]] << 1) & 0xff);
            registers[0xf] = flag;
            programCounter += 2;
        }

        private void Random(ushort[] args)
        {
            registers[args[0]] = (byte)((byte)random.Next(256) & args[1]);
            programCounter += 2;
        }
        #endregion

        #region control flow
        private void Return(ushort[] _)
        {
            programCounter = programStack.Pop();
            programCounter += 2;
        }

        private void JumpImmediate(ushort[] args)
        {
            programCounter = args[0];
        }

        private void JumpAdditive(ushort[] args)
        {
            byte arg;
            if (JumpUsesV0)
            {
                arg = registers[0];
            }
            else
            {
                byte index = (byte)((args[0] & 0xF00) >> 8);
                arg = registers[index];
            }
            programCounter = (ushort)((arg + args[0]) & 0xFFF);
        }

        private void Call(ushort[] args)
        {
            programStack.Push(programCounter);
            JumpImmediate(args);
        }

        private void SkipIfEqualImmediate(ushort[] args)
        {
            if (registers[args[0]] == args[1])
            {
                programCounter += 4;
            }
            else
            {
                programCounter += 2;
            }
        }

        private void SkipIfNotEqualImmediate(ushort[] args)
        {
            if (registers[args[0]] != args[1])
            {
                programCounter += 4;
            }
            else
            {
                programCounter += 2;
            }
        }

        private void SkipIfEqual(ushort[] args)
        {
            if (registers[args[0]] == registers[args[1]])
            {
                programCounter += 4;
            }
            else
            {
                programCounter += 2;
            }
        }

        private void SkipIfNotEqual(ushort[] args)
        {
            if (registers[args[0]] != registers[args[1]])
            {
                programCounter += 4;
            }
            else
            {
                programCounter += 2;
            }
        }
        #endregion

        #region screen
        private void ClearScreen(ushort[] _)
        {
            Array.Clear(vram);
            programCounter += 2;
            OnScreenChanged();
        }

        private void Draw(ushort[] args)
        {
            byte x = (byte)(registers[args[0]] & (SCREEN_WIDTH - 1));
            byte y = (byte)(registers[args[1]] & (SCREEN_HEIGHT - 1));
            byte n = (byte)(args[2] & 0xff);
            ushort i = I;
            registers[0xf] = 0;
            byte leftShiftAmount = (byte)(SCREEN_WIDTH - x - 8);
            byte rightShiftAmount = 0;
            if (x > 55)
            {
                leftShiftAmount = 0;
                rightShiftAmount = (byte)(x - 56);
            }
            ulong mask = (ulong)0xff << leftShiftAmount;
            for (int r = y; r < Math.Min(y + n, SCREEN_HEIGHT); r++)
            {
                ulong original = vram[r] & mask;
                ulong to_draw = (ulong)(memory[i] >> rightShiftAmount) << leftShiftAmount;
                if ((original & to_draw) != 0)
                {
                    registers[0xf] = 1;
                }
                vram[r] = (vram[r] & ~mask) | (original ^ to_draw);
                i++;
            }
            OnScreenChanged();
            programCounter += 2;
        }
        #endregion

        #region key presses
        private void SkipKeyDown(ushort[] args)
        {
            if (keys[registers[args[0]]])
            {
                programCounter += 4;
            }
            else
            {
                programCounter += 2;
            }
        }

        private void SkipKeyUp(ushort[] args)
        {
            if (!keys[registers[args[0]]])
            {
                programCounter += 4;
            }
            else
            {
                programCounter += 2;
            }
        }

        private void Input(ushort[] args)
        {
            mostRecentKeyDown = null;
            while (!mostRecentKeyDown.HasValue)
            {
                Thread.Sleep(10);
            }
            registers[args[0]] = mostRecentKeyDown.Value;
            programCounter += 2;
        }
        #endregion

        #region timers
        private void DelayCopy(ushort[] args)
        {
            registers[args[0]] = delayTimer;
            programCounter += 2;
        }

        private void Delay(ushort[] args)
        {
            delayTimer = registers[args[0]];
            programCounter += 2;
        }

        private void Sound(ushort[] args)
        {
            byte original = soundTimer;
            soundTimer = registers[args[0]];
            programCounter += 2;

            if (original == 0 && soundTimer > 1)
            {
                Task.Run(() => SoundTimerChanged?.Invoke(this, new SoundEvent(true)));
            }
        }
        #endregion

        #region memory
        private void Store(ushort[] args)
        {
            I = args[0];
            programCounter += 2;
        }

        private void AddMemory(ushort[] args)
        {
            I += registers[args[0]];
            I &= (ushort)(memory.Length - 1);
            programCounter += 2;
        }

        private void Sprite(ushort[] args)
        {
            I = (ushort)((FONT_BASE_ADDRESS + args[0] * sprites[0].Length) & 0xFFF);
            programCounter += 2;
        }

        private void BinaryCodedDecimal(ushort[] args)
        {
            byte value = (byte)(registers[args[0]] & 0xff);
            memory[I + 0] = (byte)(value / 100);
            memory[I + 1] = (byte)((value % 100) / 10);
            memory[I + 2] = (byte)(value % 10);
            programCounter += 2;
        }

        private void Save(ushort[] args)
        {
            for (int i = 0; i <= args[0]; i++)
            {
                memory[I + i] = registers[i];
            }
            if (MemoryIncrementsI)
            {
                I += (ushort)(args[0] + 1);
            }
            programCounter += 2;
        }

        private void Load(ushort[] args)
        {
            for (int i = 0; i <= args[0]; i++)
            {
                registers[i] = memory[I + i];
            }
            if (MemoryIncrementsI)
            {
                I += (ushort)(args[0] + 1);
            }
            programCounter += 2;
        }
        #endregion

        #endregion

        public void SetKey(byte keyCode, bool pressed)
        {
            if (keyCode >= keys.Length)
            {
                throw new ArgumentException($"illegal key {keyCode}", nameof(keyCode));
            }

            keys[keyCode] = pressed;

            if (!pressed) // wait for release
            {
                mostRecentKeyDown = keyCode;
            }
        }

        private void TimerTick(object? sender, ElapsedEventArgs e)
        {
            if (delayTimer > 0)
            {
                delayTimer--;
            }

            if (soundTimer > 0)
            {
                soundTimer--;

                if (soundTimer == 0)
                {
                    Task.Run(() => SoundTimerChanged?.Invoke(this, new SoundEvent(false)));
                }
            }
        }

        public void LoadProgram(string romPath)
        {
            LoadProgram(File.ReadAllBytes(romPath));
        }

        public void LoadProgram(IEnumerable<ushort> program)
        {
            var bytes = program.SelectMany(
                instruction => new byte[2] {
                    (byte)((instruction & 0xFF00) >> 8),
                    (byte)(instruction & 0x00FF)
                });
            LoadProgram(bytes);
        }

        public void LoadProgram(IEnumerable<byte> program)
        {
            byte[] data = program.ToArray();

            if (data.Length + PROGRAM_START_ADDRESS > MEMORY_SIZE_BYTES)
            {
                throw new ArgumentException("program is too big to fit in memory");
            }
            running = false;
            Array.Clear(memory, PROGRAM_START_ADDRESS, memory.Length - PROGRAM_START_ADDRESS);
            Array.Copy(data, 0, memory, PROGRAM_START_ADDRESS, data.Length);
        }

        public void Execute()
        {
            Reset();

            timer.Start();

            running = true;

            uint n = 0;
            while (running && programCounter < MEMORY_SIZE_BYTES)
            {
                ushort value = CurrentInstruction;

                if (value == 0)
                {
                    break;
                }

                Instruction instruction = new(value);
                // Trace.WriteLine($"{n}: PC = 0x{programCounter:X}, {instruction.Code}");
                functionMap[instruction.OpCode](instruction.Arguments);

                n++;
            }

            timer.Stop();
        }

        public void DumpState(TextWriter? writer = null)
        {
            writer ??= Console.Out;

            writer.WriteLine($"PC = 0x{programCounter:x}");
            Instruction instruction = new(CurrentInstruction);
            writer.WriteLine($"Instruction = 0x{CurrentInstruction:x} ({instruction.Code})");
            writer.WriteLine($"Registers = {string.Join(", ", registers.Select(r => $"0x{r:x}"))}");
            writer.WriteLine($"I = 0x{I:x}");
            writer.WriteLine($"Stack = {string.Join(", ", programStack.Select(a => $"0x{a:x}"))}");
            writer.WriteLine($"Delay = 0x{delayTimer:x}");
            writer.WriteLine($"Sound = 0x{soundTimer:x}");
            writer.WriteLine($"Key State = {keys.Select(up => up ? "^" : "v")}");
            writer.WriteLine($"Last key pressed = 0x{mostRecentKeyDown?.ToString("x") ?? "none"}");
        }

        public void DumpScreen(TextWriter? writer = null)
        {
            writer ??= Console.Out;

            writer.WriteLine("   0123456789112345678921234567893123456789412345678951234567896123");
            string sep = "  +" + new string('-', 64) + "+";
            writer.WriteLine(sep);
            int r = 0;
            foreach (ulong row in vram)
            {
                string rep = Convert.ToString((long)row, 2).PadLeft(64, '0');
                writer.WriteLine(r.ToString("D2") + '|' + string.Join("", rep.Select(ch => ch == '1' ? '#' : ' ')) + '|');
                r++;
            }
            writer.WriteLine(sep);
        }

        private void Reset()
        {
            Array.Clear(registers);
            ClearScreen([]);
            programStack.Clear();
            programCounter = PROGRAM_START_ADDRESS;
        }

        private void OnScreenChanged()
        {
            ulong[] copy = new ulong[vram.Length];
            Array.Copy(vram, copy, vram.Length);

            ScreenUpdated?.Invoke(this, new ScreenEvent(copy));
        }

        #region events
        public class SoundEvent(bool on) : EventArgs
        {
            public bool On { get; private set; } = on;
        }

        public class ScreenEvent(ulong[] screen) : EventArgs
        {
            public ulong[] Screen { get; private set; } = screen;
        }
        #endregion
    }
}
