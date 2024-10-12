using System.Text.RegularExpressions;

namespace emulator
{
    public class Assembler
    {
        /*
         * program := statement+
         * statement := function | label | instruction | data
         * function := FUNC NAME NEWLINE '{' NEWLINE (label | instruction)+ '}' NEWLINE
         * label := NAME NEWLINE 
         * instruction := "CLR" NEWLINE
         *              | "DRAW" REG REG NIBBLE NEWLINE
         *              | addressableInstruction
         *              | unaryInstruction
         *              | regArgInstruction
         *              | regRegInstruction
         * addressableInstruction := ("JMPI" | "JMPA") ADDRESS NEWLINE
         *                         | "CALL" ADDRESS NEWLINE
         *                         | "STO" ADDRESS NEWLINE
         * unaryInstruction := ("SKEQ" | "SKNE" | "DCPY" | "INP") REG NEWLINE
         *                   | ("DLY" | "SND" | "SAVE" | "LD") REG NEWLINE
         *                   | ("ADDM" | "SPR" | "BCD") REG NEWLINE
         * regArgInstruction := "SEQI" REG ARG NEWLINE
         *                    | "SNEI" REG ARG NEWLINE
         *                    | "SETI" REG ARG NEWLINE
         *                    | "ADDI" REG ARG NEWLINE
         *                    | "RAND" REG ARG NEWLINE
         * regRegInstruction := "SEQ" REG REG NEWLINE
         *                    | ("SET" | "OR" | "AND" | "XOR" | "ADD") REG REG NEWLINE
         *                    | ("SUB" | "RSH" | "RSUB" | "LSH" | "SNE") REG REG NEWLINE
         * data := DATA NAME NEWLINE '{' datum+ '}' NEWLINE
         * datum := WORD NEWLINE
         * REG := 'v' [0-9a-f]
         * NIBBLE := num < 0x10
         * ARG := num < 0x100
         * ADDRESS := num < 0x1000
         * WORD := num < 0x10000
         * NAME := [a-z][a-z0-9_]*
         * NEWLINE := '\n' | '\r' | '\r\n'
         * FUNC := 'func'
         * DATA := 'data'
         */

        #region literals and regexes
        private static readonly HashSet<string> mnemonics = [
            "CLR", "JMPI", "CALL", "SEQI", "SNEI", "SEQ", "SETI", "ADDI", "SET", "OR", "AND", "XOR",
            "ADD", "SUB", "RSH", "RSUB", "LSH", "SNE", "STO", "JMPA", "RAND", "DRAW", "SKEQ", "SKNE",
            "DCPY", "INP", "DLY", "SND", "SAVE", "LD", "ADDM", "SPR", "BCD"];
        private static readonly HashSet<string> addressableMnemonics = ["JMPI", "CALL", "STO", "JMPA"];
        private static readonly HashSet<string> unaryMnemonics = ["SKEQ", "SKNE", "DCPY", "INP",
            "DLY", "SND", "SAVE", "LD", "ADDM", "SPR", "BCD"];
        private static readonly HashSet<string> regRegMnemonics = ["SEQ", "SET", "OR", "AND", "XOR", "ADD",
            "SUB", "RSH", "RSUB", "LSH", "SNE"];
        private static readonly HashSet<string> regArgMnemonics = ["SEQI", "SNEI", "SETI", "ADDI", "RAND"];
        private const string functionLiteralRegex = "^func$";
        private const string dataLiteralRegex = "^data$";
        private const string nameLiteralRegex = "^[a-z][a-z0-9_]+$";
        private const string decimalNumberLiteralRegex = "^[1-9][0-9]*$";
        private const string binaryNumberLiteralRegex = "^0b[01]+$";
        private const string hexNumberLiteralRegex = "^0x[0-9a-f]+$";
        private const string registerLiteralRegex = "^v[0-9a-fA-F]$";
        private const string entryPointLiteralRegex = "^(start|main)$";
        #endregion

        private readonly List<Token> tokens = [];
        private readonly List<Statement> statements = [];
        private readonly HashSet<string> labelNames = [];
        private readonly HashSet<string> subroutineNames = [];
        private readonly HashSet<string> dataNames = [];
        private readonly List<ushort> program = [];
        private readonly Dictionary<string, ushort> labelAddresses = [];
        private readonly Dictionary<string, ushort> functionAddresses = [];
        private readonly Dictionary<string, ushort> dataAddresses = [];
        private readonly Dictionary<ushort, InstructionStatement> backpatches = [];
        
        private string? entryName;
        private int pos;

        public void Assemble(string srcPath, string destPath)
        {
            Reset();

            Lex(srcPath);
            Parse();
            Compile();
            Emit(destPath);
        }

        private void Reset()
        {
            pos = 0;
            tokens.Clear();
            labelNames.Clear();
            subroutineNames.Clear();
            statements.Clear();
            dataNames.Clear();
            program.Clear();
            labelAddresses.Clear();
            functionAddresses.Clear();
            dataAddresses.Clear();
            backpatches.Clear();
        }

        private void Lex(string filepath)
        {
            using (var reader = new StreamReader(filepath))
            {
                int lineno = 1;
                while (!reader.EndOfStream)
                {
                    string? line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    int commentStart = line.IndexOf('#');
                    if (commentStart > 0)
                    {
                        line = line[..commentStart];
                    }
                    foreach (string token in line.Replace('\t', ' ').Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (Regex.IsMatch(token, functionLiteralRegex, RegexOptions.IgnoreCase))
                        {
                            tokens.Add(new Token(lineno, token, TokenKind.FUNCTION));
                        }
                        else if (Regex.IsMatch(token, dataLiteralRegex, RegexOptions.IgnoreCase))
                        {
                            tokens.Add(new Token(lineno, token, TokenKind.DATA));
                        }
                        else if (token == "{")
                        {
                            tokens.Add(new Token(lineno, token, TokenKind.LEFT_CURLY));
                        }
                        else if (token == "}")
                        {
                            tokens.Add(new Token(lineno, token, TokenKind.RIGHT_CURLY));
                        }
                        else if (mnemonics.Contains(token.ToUpper()))
                        {
                            tokens.Add(new Token(lineno, token.ToUpper(), TokenKind.MNEMONIC));
                        }
                        else if (Regex.IsMatch(token, registerLiteralRegex))
                        {
                            tokens.Add(new Token(lineno,
                                                 Convert.ToByte(token[1].ToString(), 16),
                                                 TokenKind.REGISTER));
                        }
                        else if (Regex.IsMatch(token, binaryNumberLiteralRegex, RegexOptions.IgnoreCase))
                        {
                            tokens.Add(new Token(lineno,
                                                 Convert.ToUInt16(token[2..], 2),
                                                 TokenKind.NUMBER));
                        }
                        else if (Regex.IsMatch(token, decimalNumberLiteralRegex, RegexOptions.IgnoreCase))
                        {
                            tokens.Add(new Token(lineno,
                                                 Convert.ToUInt16(token),
                                                 TokenKind.NUMBER));
                        }
                        else if (Regex.IsMatch(token, hexNumberLiteralRegex, RegexOptions.IgnoreCase))
                        {
                            tokens.Add(new Token(lineno,
                                                 Convert.ToUInt16(token[2..], 16),
                                                 TokenKind.NUMBER));
                        }
                        else if (Regex.IsMatch(token, nameLiteralRegex, RegexOptions.IgnoreCase))
                        {
                            tokens.Add(new Token(lineno, token.ToUpper(), TokenKind.NAME));
                        }
                        else
                        {
                            throw new ParseException($"line {lineno}: unknown token '{token}'");
                        }
                    }

                    tokens.Add(new Token(lineno, string.Empty, TokenKind.NEWLINE));
                    lineno++;
                }
            }

            List<Token> coalescedTokens = [];
            int i = 0;
            while (i < tokens.Count)
            {
                coalescedTokens.Add(tokens[i]);
                if (tokens[i].Kind == TokenKind.NEWLINE)
                {
                    while (i + 1 < tokens.Count && tokens[i+1].Kind == TokenKind.NEWLINE)
                    {
                        i++;
                    }
                }

                i++;
            }

            tokens.Clear();
            tokens.AddRange(coalescedTokens);
        }

        private void Parse()
        {
            while(pos < tokens.Count)
            {
                switch (tokens[pos].Kind)
                {
                    case TokenKind.NAME:
                        LabelStatement label = ParseLabel();
                        statements.Add(label);
                        break;
                    case TokenKind.MNEMONIC:
                        InstructionStatement instruction = ParseInstruction();
                        statements.Add(instruction);
                        break;
                    case TokenKind.FUNCTION:
                        Subroutine subroutine = ParseFunction();
                        statements.Add(subroutine);
                        break;
                    case TokenKind.DATA:
                        DataSegment data = ParseData();
                        statements.Add(data);
                        break;
                    default:
                        throw new ParseException(tokens[pos], "unknown statement");
                }
            }
            
            if (pos < tokens.Count - 1)
            {
                throw new ParseException(tokens[pos], "unconsumed statements");
            }

            entryName = labelNames.SingleOrDefault(name => Regex.IsMatch(name, entryPointLiteralRegex, RegexOptions.IgnoreCase)) ??
                throw new ParseException("program must have an entrypoint");
        }
        
        #region parsing helpers
        private Token Expect(TokenKind kind)
        {
            if (tokens[pos].Kind != kind)
            {
                throw new ParseException(tokens[pos], $"expected {kind}, found {tokens[pos].Kind}");
            }
            return tokens[pos++];
        }

        private Subroutine ParseFunction()
        {
            Expect(TokenKind.FUNCTION);
            Token token = Expect(TokenKind.NAME);
            string name = token.Value?.ToString() ?? throw new ParseException(token, "function must have name");
            if (subroutineNames.Contains(name))
            {
                throw new ParseException(token, $"function '{name}' already declared");
            }
            Subroutine subroutine = new(name);
            Expect(TokenKind.NEWLINE);
            Expect(TokenKind.LEFT_CURLY);
            while (tokens[pos].Kind != TokenKind.RIGHT_CURLY)
            {
                if (tokens[pos].Kind == TokenKind.NAME)
                {
                    LabelStatement label = ParseLabel();
                    subroutine.statements.Add(label);
                }
                else if (tokens[pos].Kind == TokenKind.MNEMONIC)
                {
                    InstructionStatement instruction = ParseInstruction();
                    subroutine.statements.Add(instruction);
                }
                else
                {
                    throw new ParseException(tokens[pos], "unknown statement inside function");
                }
            }
            Expect(TokenKind.RIGHT_CURLY);
            Expect(TokenKind.NEWLINE);
            if (subroutine.statements.Count == 0)
            {
                throw new ParseException(token, $"subroutine {name} must have at least one statement");
            }

            subroutineNames.Add(name);

            return subroutine;
        }

        private LabelStatement ParseLabel()
        {
            Token token = Expect(TokenKind.NAME);
            string name = token.Value.ToString() ?? throw new ParseException(token, "label name is null");
            Expect(TokenKind.NEWLINE);
            if (labelNames.Contains(name))
            {
                throw new ParseException(token, $"label name '{name}' already declared");
            }
            LabelStatement label = new(name);
            labelNames.Add(name);
            return label;
        }

        private InstructionStatement ParseInstruction()
        {
            Token token = Expect(TokenKind.MNEMONIC);
            InstructionStatement instruction;
            string mnemonic = token.Value?.ToString() ??
                throw new ParseException(token, "token value is null");
            if (mnemonic == "CLR")
            {
                instruction = new(mnemonic, []);
            }
            else if (mnemonic == "DRAW")
            {
                instruction = new(mnemonic, [ParseRegister(), ParseRegister(), ParseNumber(4)]);
            }
            else if (unaryMnemonics.Contains(mnemonic))
            {
                instruction = new(mnemonic, [ParseRegister()]);
            }
            else if (addressableMnemonics.Contains(mnemonic))
            {
                Token name = Expect(TokenKind.NAME);
                instruction = new(mnemonic, name.Value.ToString() ?? throw new ParseException(name, "name has no value"));
            }
            else if (regArgMnemonics.Contains(mnemonic))
            {
                instruction = new(mnemonic, [ParseRegister(), ParseNumber(8)]);
            }
            else if (regRegMnemonics.Contains(mnemonic))
            {
                instruction = new(mnemonic, [ParseRegister(), ParseRegister()]);
            }
            else
            {
                throw new ParseException(token, "unknown mnemonic");
            }

            Expect(TokenKind.NEWLINE);

            return instruction;
        }

        private byte ParseRegister()
        {
            Token token = Expect(TokenKind.REGISTER);
            byte? b = token.Value as byte?;
            if (!b.HasValue)
            {
                throw new ParseException(token, "expected register number, found nothing");
            }
            return b.Value;
        }

        private ushort ParseNumber(byte numBits)
        {
            Token token = Expect(TokenKind.NUMBER);
            ushort? number = token.Value as ushort?;
            if (!number.HasValue)
            {
                throw new ParseException(token, $"expected number, found {token.Kind}");
            }
            if (number.Value > (ushort)(Math.Pow(2, numBits) - 1))
            {
                throw new ParseException(token, $"expected {numBits}-bit number");
            }

            return number.Value;
        }

        private DataSegment ParseData()
        {
            Expect(TokenKind.DATA);
            Token token = Expect(TokenKind.NAME);
            string name = token.Value?.ToString() ?? throw new ParseException(token, "data must have name");
            if (dataNames.Contains(name))
            {
                throw new ParseException(token, $"data name '{name}' already declared");
            }
            DataSegment data = new(name);
            Expect(TokenKind.NEWLINE);
            Expect(TokenKind.LEFT_CURLY);
            Expect(TokenKind.NEWLINE);
            while (tokens[pos].Kind != TokenKind.RIGHT_CURLY)
            {
                ushort number = ParseNumber(16);
                data.data.Add(number);
                Expect(TokenKind.NEWLINE);
            }
            Expect(TokenKind.RIGHT_CURLY);
            Expect(TokenKind.NEWLINE);
            if (data.data.Count == 0)
            {
                throw new ParseException(token, $"data segment {name} must have at least one datum");
            }
            return data;
        }
        #endregion
        
        private void Compile()
        {
            int i = 0;
            ushort offset = 0;

            program.Add(0x0000);
            backpatches.Add(0, new InstructionStatement("JMPI", entryName ?? throw new ParseException("no entry point")));
            offset += 2;

            foreach (Statement stmt in statements)
            {
                if (stmt is LabelStatement)
                {
                    labelAddresses[stmt.Name] = offset;
                }
                else if (stmt is Subroutine subroutine)
                {
                    functionAddresses[stmt.Name] = offset;
                    foreach (Statement inner in subroutine.statements)
                    {
                        if (inner is LabelStatement)
                        {
                            labelAddresses[inner.Name] = offset;
                        }
                        else if (inner is InstructionStatement instruction)
                        {
                            CompileInstruction(instruction, ref offset);
                        }
                    }
                    program.Add(new Instruction("RET", []).Value);
                }
                else if (stmt is DataSegment data)
                {
                    dataAddresses[stmt.Name] = offset;
                    foreach(ushort datum in data.data)
                    {
                        program.Add(datum);
                        offset += 2;
                    }
                }
                else if (stmt is InstructionStatement instruction)
                {
                    CompileInstruction(instruction, ref offset);
                }
                else
                {
                    throw new ParseException($"unknown statement '{stmt.Name}'");
                }

                i++;
            }

            foreach((ushort o, InstructionStatement instruction) in backpatches)
            {
                Backpatch(instruction, o >> 1);
            }    
        }

        #region compile helpers
        private void CompileInstruction(InstructionStatement instruction, ref ushort offset)
        {
            if (instruction.NeedsAddressResolution)
            {
                program.Add(0x0000);
                backpatches.Add(offset, instruction);
            }
            else
            {
                program.Add(instruction.instruction?.Value ?? throw new ParseException("instruction is null"));
            }

            offset += 2;
        }

        private void Backpatch(InstructionStatement instruction, int index)
        {
            string name = instruction.name ?? throw new ParseException("addressable instruction without name");
            Instruction i = instruction.mnemonic switch
            {
                "JMPI" => ResolveInstruction(instruction, labelAddresses, name),
                "JMPA" => ResolveInstruction(instruction, labelAddresses, name),
                "STO" => ResolveInstruction(instruction, dataAddresses, name),
                "CALL" => ResolveInstruction(instruction, functionAddresses, name),
                _ => throw new ParseException($"illegal instruction needing address resolution {instruction.mnemonic}")
            };
            program[index] = i.Value;
        }

        private static Instruction ResolveInstruction(InstructionStatement instruction,
            Dictionary<string, ushort> lookup,
            string name)
        {
            if (lookup.TryGetValue(name, out ushort address))
            {
                return new(instruction.mnemonic, [(ushort)(address + Chip8.PROGRAM_START_ADDRESS)]);
            }
            else
            {
                throw new ParseException($"never found segment with name '{name}'");
            }
        }
        #endregion

        private void Emit(string destPath)
        {
            byte[] bytes = program
                .SelectMany(word => new byte[2] {
                    (byte)((word & 0xFF00) >> 8),
                    (byte)(word & 0x00FF)
                }).ToArray();
            File.WriteAllBytes(destPath, bytes);
        }

        private readonly struct Token(int line, object value, TokenKind kind)
        {
            public int Line { get; } = line; // 1-based
            public object Value { get; } = value;
            public TokenKind Kind { get; } = kind;

            public override string ToString()
            {
                return $"line {Line} '{Value}' {Kind}";
            }
        }

        private enum TokenKind
        {
            UNKNOWN,
            LEFT_CURLY,
            RIGHT_CURLY,
            NEWLINE,
            NAME,
            REGISTER,
            NUMBER,
            FUNCTION,
            MNEMONIC,
            DATA,
        }

        private class ParseException(string message) : Exception(message)
        {
            public ParseException(Token token, string message) :
                this($"line {token.Line}: '{token.Value}' - {message}")
            {

            }
        }

        #region AST objects
        private abstract class Statement(string? name)
        {
            public string Name { get; } = name ?? throw new Exception();
        }

        private class DataSegment(string name) : Statement(name)
        {
            public readonly List<ushort> data = [];
        }

        private class LabelStatement(string name) : Statement(name)
        {
        }

        private class InstructionStatement : Statement
        {
            public readonly Instruction? instruction;
            public readonly string mnemonic;
            public readonly string? name;
            public bool NeedsAddressResolution => name != null;

            public InstructionStatement(string mnemonic, ushort[] args) : base(mnemonic)
            {
                instruction = new Instruction(mnemonic, args);
                this.mnemonic = mnemonic;
                name = null;
            }

            public InstructionStatement(string mnemonic, string name) : base(mnemonic)
            {
                instruction = null;
                this.mnemonic = mnemonic;
                this.name = name;
            }
        }

        private class Subroutine(string name) : Statement(name)
        {
            public readonly List<Statement> statements = [];
        }
        #endregion
    }
}
