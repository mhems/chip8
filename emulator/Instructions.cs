using System.Collections.ObjectModel;
using System.Text;

namespace emulator
{
    public enum LiteralSize
    {
        Four = 0xf,
        Eight = 0xff,
        Twelve = 0xfff,
    }

    public readonly struct ArgumentFormat
    {
        public bool Immediate { get; }
        public LiteralSize Size { get; }

        public ArgumentFormat() : this(false, LiteralSize.Four) { }

        public ArgumentFormat(LiteralSize size) : this(true, size) { }

        private ArgumentFormat(bool immediate, LiteralSize size)
        {
            this.Immediate = immediate;
            this.Size = size;
        }
    }

    public readonly struct InstructionFormat
    {
        public string Opcode { get; }
        public string Mnemonic { get; }
        public uint Arity { get; }
        public ArgumentFormat[] Arguments { get; }

        public InstructionFormat(string opcode, string mnemonic, IEnumerable<ArgumentFormat> arguments)
        {
            this.Opcode = opcode;
            this.Mnemonic = mnemonic;
            this.Arguments = new List<ArgumentFormat>(arguments.ToList()).ToArray();
            this.Arity = (uint)this.Arguments.Length;
        }

        public override readonly string ToString()
        {
            return $"{Opcode} ({Mnemonic}/{Arity})";
        }
    }

    public readonly struct Instruction
    {
        private static readonly HashSet<char> reserved = ['X', 'Y', 'N'];

        public ushort Value { get; }
        public ushort[] Arguments { get; }
        public string Code { get; }
        public string OpCode { get; }
        public string Mnemonic { get; }

        public Instruction(ushort value)
        {
            Value = value;
            string s = value.ToString("X4");
            OpCode = Instructions.opCodes.Where(pair => Match(pair.opcode, s)).SingleOrDefault().opcode;
            if (OpCode == null)
            {
                throw new ArgumentException($"malformed instruction 0x{value:x}", nameof(value));
            }
            InstructionFormat format = Instructions.InstructionFormatMap[OpCode];
            Mnemonic = format.Mnemonic;
            StringBuilder codeBuilder = new(format.Mnemonic);
            Arguments = [];
            if (format.Arity > 0)
            {
                List<ushort> args = [];
                codeBuilder.Append(' ');

                int xIndex = OpCode.IndexOf('X');
                if (xIndex != -1)
                {
                    args.Add(ParseRegister(value, xIndex));
                }

                int yIndex = OpCode.IndexOf('Y');
                if (yIndex != -1)
                {
                    args.Add(ParseRegister(value, yIndex));
                }

                int nCount = OpCode.Reverse().TakeWhile(c => c == 'N').Count();
                if (nCount > 0)
                {
                    args.Add(ParseImmediate(value, nCount));
                }

                Arguments = [.. args];
                codeBuilder.Append(string.Join(' ', args.Select(a => $"0x{a:X}")));
            }
            Code = codeBuilder.ToString();
        }

        public Instruction(string mnemonic, ushort[] args)
        {
            OpCode = Instructions.opCodes.Where(pair => pair.mnemonic == mnemonic).Single().opcode;
            Mnemonic = mnemonic;
            StringBuilder codeBuilder = new(mnemonic);
            if (args.Length > 0)
            {
                codeBuilder.Append(' ');
                codeBuilder.Append(string.Join(' ', args.Select(a => $"0x{a:X}")));
            }
            Code = codeBuilder.ToString();
            Arguments = args;
            Value = 0;
            int nCount = OpCode.Reverse().TakeWhile(c => c == 'N').Count();
            int argIndex = 0;
            for (int i = 0; i < OpCode.Length; i++)
            {
                if (reserved.Contains(OpCode[i]))
                {
                    if (OpCode[i] == 'N')
                    {
                        Value |= ParseImmediate(args[argIndex], nCount);
                        break;
                    }
                    Value |= (ushort)((args[argIndex] & 0xF) << (4 * (4 - 1 - i)));
                    argIndex++;
                }
                else
                {
                    Value |= (ushort)((Convert.ToUInt16(OpCode[i].ToString(), 16) & 0xF) << (4 * (4-1-i)));
                }
            }
        }

        public override readonly string ToString()
        {
            return $"{Value:X4}";
        }

        private static bool Match(string opcode, string candidate)
        {
            if (opcode.Length == candidate.Length)
            {
                foreach ((char o, char c) in opcode.Zip(candidate))
                {
                    if (!reserved.Contains(o) && c != o)
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        private static readonly ushort[] registerMasks = [0, 0x0f00, 0x00f0, 0x000f];
        private static byte ParseRegister(ushort opcode, int xIndex)
        {
            return (byte)((opcode & registerMasks[xIndex]) >> (4 * (4 - 1 - xIndex)));
        }

        private static ushort ParseImmediate(ushort immediate, int nCount)
        {
            return (ushort)(immediate & (ushort)(Math.Pow(2, 4 * nCount) - 1));
        }
    }


    public class Instructions
    {
        private static readonly LiteralSize[] sizes = [LiteralSize.Four, LiteralSize.Eight, LiteralSize.Twelve];

        internal static readonly (string opcode, string mnemonic)[] opCodes =
        [
            ("00E0", "CLR"), 
            ("00EE", "RET"), 
            ("1NNN", "JMPI"),
            ("2NNN", "CALL"), 
            ("3XNN", "SEQI"),
            ("4XNN", "SNEI"),
            ("5XY0", "SEQ"), 
            ("6XNN", "SETI"),
            ("7XNN", "ADDI"),
            ("8XY0", "SET"), 
            ("8XY1", "OR"),
            ("8XY2", "AND"), 
            ("8XY3", "XOR"), 
            ("8XY4", "ADD"), 
            ("8XY5", "SUB"), 
            ("8XY6", "RSH"), 
            ("8XY7", "RSUB"),
            ("8XYE", "LSH"), 
            ("9XY0", "SNE"), 
            ("ANNN", "STO"), 
            ("BNNN", "JMPA"),
            ("CXNN", "RAND"),
            ("DXYN", "DRAW"),
            ("EX9E", "SKEQ"),
            ("EXA1", "SKNE"),
            ("FX07", "DCPY"),
            ("FX0A", "INP"),
            ("FX15", "DLY"),
            ("FX18", "SND"),
            ("FX1E", "ADDM"),
            ("FX29", "SPR"),
            ("FX33", "BCD"),
            ("FX55", "SAVE"),
            ("FX65", "LD"),
        ];

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2211:Non-constant fields should not be visible", Justification = "dictionary is readonly")]
        public static ReadOnlyDictionary<string, InstructionFormat> InstructionFormatMap = new (opCodes
            .Select(op => (op.opcode, Parse(op.opcode, op.mnemonic)))
            .ToDictionary(pair => pair.opcode, pair => pair.Item2));

        private static InstructionFormat Parse(string opcode, string mnemonic)
        {
            List<ArgumentFormat> args = [];
            uint numN = 0;

            foreach (char c in opcode)
            {
                if (c == 'X' || c == 'Y')
                {
                    args.Add(new ArgumentFormat());
                }
                else if (c == 'N')
                {
                    numN++;
                }
            }

            if (numN > 0)
            {
                if (numN > 3)
                {
                    throw new FormatException($"opcode {opcode} ({mnemonic}) has more than 3 'N's");
                }

                args.Add(new ArgumentFormat(sizes[numN - 1]));
            }

            return new InstructionFormat(opcode, mnemonic, args);
        }
    }
}
