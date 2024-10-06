using System;
using System.Timers;
using System.Diagnostics;
using System.Xml.Schema;
using System.Net.Http.Headers;

namespace emulator
{
    public static class Assembler
    {
        public static void Disassemble(string srcPath, string destPath)
        {
            Disassemble(File.ReadAllBytes(srcPath), destPath);
        }

        public static void Disassemble(IEnumerable<ushort> program, string destPath)
        {
            ushort[] programArray = program.ToArray();

            Dictionary<ushort, string> functions = new();
            HashSet<ushort> returns = new();
            Dictionary<ushort, string> labels = new();
            HashSet<ushort> reachable = new();
            Stack<ushort> stack = new();
#if false
            using (StreamWriter writer = File.CreateText("output.8o"))
            {
                ushort address = Chip8.PROGRAM_START_ADDRESS;
                foreach(ushort datum in programArray)
                {
                    writer.Write($"0x{address:x4}: ");
                    try
                    {
                        writer.WriteLine($"{new Instruction(datum).Code}");
                    }
                    catch
                    {
                        writer.WriteLine($"0x{datum:x4}");
                    }
                    address += 2;
                }
            }
#endif

            void Trace(ushort start_address, bool label=false, bool func=false)
            {
                ushort address = start_address;
                uint index = (ushort)(address >> 1);
                while (index < programArray.Length)
                {
                    reachable.Add(address);
                    Instruction instruction = new(programArray[index]);
                    Debug.WriteLine($"{address:x4} {instruction.Code}");
                    switch (instruction.Mnemonic)
                    {
                        case "CALL":
                            if (func)
                            {
                                throw new NotImplementedException("cannot handle nested calls yet");
                            }
                            stack.Push(address);
                            Trace((ushort)(instruction.Arguments[0] - Chip8.PROGRAM_START_ADDRESS), label = false, func = true);
                            address = stack.Pop();
                            break;
                        case "RET":
                            if (!func)
                            {
                                throw new Exception("illegal return from non-function context");
                            }
                            functions.Add(start_address, $"func{functions.Count + 1}");
                            returns.Add(address);
                            return;
                        case "JMPI":
                            labels.Add((ushort)(instruction.Arguments[0] - Chip8.PROGRAM_START_ADDRESS), $"label{labels.Count + 1}");
                            if (instruction.Arguments[0] != address + Chip8.PROGRAM_START_ADDRESS)
                            {
                                Trace((ushort)(instruction.Arguments[0] - Chip8.PROGRAM_START_ADDRESS), label = true, func = false);
                            }
                            return; // jump is one-way
                        case "SEQI":
                        case "SNEI":
                        case "SEQ":
                        case "SNE":
                        case "SKEQ":
                        case "SKNE":
                            // fallthrough, cannot predict which branch is taken
                        case "JMPA":
                            // fallthrough, cannot predict jump target
                        default:
                            break;
                    }
                    if (instruction.Code == "SETI 0x6 0xF")
                    {
                        Debug.WriteLine("");
                    }
                    address += 2;
                    index++;
                }
            }

            Trace(0);

            using (StreamWriter writer = File.CreateText(destPath))
            {
                ushort address = 0;
                uint index = 0;
                while (index < programArray.Length)
                {
                    if (functions.TryGetValue(address, out string? name))
                    {
                        writer.WriteLine($"Function {name}");
                    }
                    else if (labels.TryGetValue(address, out string? label))
                    {
                        writer.WriteLine($"Label {label}");
                    }

                    writer.Write($"0x{(address + Chip8.PROGRAM_START_ADDRESS):x4}: ");
                    if (reachable.Contains(address))
                    {
                        Instruction instruction = new(programArray[index]);
                        switch (instruction.Mnemonic)
                        {
                            case "CALL":
                                writer.Write($"CALL {functions[(ushort)(instruction.Arguments[0] - Chip8.PROGRAM_START_ADDRESS)]}");
                                break;
                            case "JMPI":
                                writer.Write($"JMPI {labels[(ushort)(instruction.Arguments[0] - Chip8.PROGRAM_START_ADDRESS)]}");
                                break;
                            default:
                                writer.Write($"{instruction.Code:x4}");
                                break;
                        }
                    }
                    else
                    {
                        writer.Write($"0x{programArray[index]:x4}");
                    }

                    writer.WriteLine();

                    address += 2;
                    index++;
                }
            }
        }

        public static void Disassemble(IEnumerable<byte> program, string destPath)
        {
            List<ushort> words = new();
            List<byte> programData = new(program);
            if (programData.Count % 2 == 1)
            {
                programData.Add(0);
            }
            for (int i = 0; i < programData.Count; i+= 2)
            {
                ushort datum = (ushort)((((ushort)programData[i]) << 8) | (ushort)programData[i+1]);
                words.Add(datum);
            }
            Disassemble(words, destPath);
        }
    }
}
