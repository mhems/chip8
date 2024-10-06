namespace emulator
{
    public static class Disassembler
    {
        public static void Disassemble(string srcPath, string destPath)
        {
            Disassemble(File.ReadAllBytes(srcPath), destPath);
        }

        public static void Disassemble(IEnumerable<byte> program, string destPath)
        {
            List<ushort> words = [];
            List<byte> programData = new(program);
            if (programData.Count % 2 == 1)
            {
                programData.Add(0);
            }
            for (int i = 0; i < programData.Count; i += 2)
            {
                ushort datum = (ushort)((programData[i] << 8) | programData[i + 1]);
                words.Add(datum);
            }
            Disassemble(words, destPath);
        }

        public static void Disassemble(IEnumerable<ushort> program, string destPath)
        {
            ushort[] programArray = program.ToArray();

            Dictionary<ushort, string> functions = [];
            HashSet<ushort> returns = [];
            Dictionary<ushort, string> labels = [];
            HashSet<ushort> reachable = [];
            HashSet<ushort> traced = [];

            void Trace(ushort start_address)
            {
                traced.Add(start_address);
                ushort address = start_address;
                uint index = (ushort)(address >> 1);
                ushort target;
                while (index < programArray.Length)
                {
                    Instruction instruction;
                    try
                    {
                        instruction = new(programArray[index]);
                    }
                    catch(ArgumentException)
                    {
                        return;
                    }
                    reachable.Add(address);

                    switch (instruction.Mnemonic)
                    {
                        case "CALL":
                            target = (ushort)(instruction.Arguments[0] - Chip8.PROGRAM_START_ADDRESS);
                            if (!functions.ContainsKey(target))
                            {
                                functions.Add(target, $"func{functions.Count + 1}");
                                Trace(target);
                            }
                            break;
                        case "RET":
                            returns.Add(address);
                            return;
                        case "JMPI":
                            target = (ushort)(instruction.Arguments[0] - Chip8.PROGRAM_START_ADDRESS);
                            if (!labels.ContainsKey(target))
                            {
                                labels.Add(target, $"label{labels.Count + 1}");
                                if (instruction.Arguments[0] != address + Chip8.PROGRAM_START_ADDRESS)
                                {
                                    Trace(target);
                                }
                            }
                            return;
                        case "SEQI":
                        case "SNEI":
                        case "SEQ":
                        case "SNE":
                        case "SKEQ":
                        case "SKNE":
                            target = (ushort)(address + 4);
                            if (!traced.Contains(target))
                            { 
                                Trace(target);
                            }
                            break;
                        case "JMPA":
                            // fallthrough, cannot predict jump target
                        default:
                            break;
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
    }
}
