using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace emulator
{
    public class Assembler
    {
        /*
         * program := statement+
         * statement := function | label | instruction | data
         * function := FUNC NAME NEWLINE '{' NEWLINE statement+ '}' NEWLINE
         * label := NAME NEWLINE 
         * instruction := "CLR" NEWLINE
         *              | "DRAW" REG REG NIBBLE NEWLINE
         *              | addressableInstruction
         *              | unaryInstruction
         *              | regArgInstruction
         *              | regRegInstruction
         * addressableInstruction := "JMPI" (ADDRESS | NAME) NEWLINE
         *                         | "CALL" (ADDRESS | NAME) NEWLINE
         *                         | ("STO | "JMPA") (ADDRESS | NAME) NEWLINE
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
        private const string functionLiteralRegex = "func";
        private const string dataLiteralRegex = "data";
        private const string nameLiteralRegex = "[a-z][a-z0-9_]+";
        private const string decimalNumberLiteralRegex = "[1-9][0-9]*";
        private const string binaryNumberLiteralRegex = "0b[01]+";
        private const string hexNumberLiteralRegex = "0x[0-9a-f]+";
        private const string registerLiteralRegex = "v[0-9a-fA-F]";
        private const string entryPointLiteralRegex = "start|main";
        #endregion

        private readonly List<Token> tokens = [];
        private readonly HashSet<LabelStatement> labels = [];
        private readonly HashSet<Subroutine> subroutines = [];
        private int pos;

        public void Assemble(string srcPath, string destPath)
        {
            Reset();

            Lex(srcPath);
            // TODO coalesce adjacent newlines
            var ast = Parse();
            var code = Compile(ast);
            Emit(code, destPath);
        }

        private void Reset()
        {
            pos = 0;
            tokens.Clear();
            labels.Clear();
            subroutines.Clear();
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
                    foreach (string token in line.Replace('\t', ' ').Split(' '))
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
                        else if (Regex.IsMatch(token, nameLiteralRegex, RegexOptions.IgnoreCase))
                        {
                            tokens.Add(new Token(lineno, token.ToUpper(), TokenKind.NAME));
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
                        else
                        {
                            throw new Exception($"line {lineno}: unknown token '{token}'");
                        }
                    }

                    tokens.Add(new Token(lineno, string.Empty, TokenKind.NEWLINE));
                    lineno++;
                }
            }
        }

        private object Parse()
        {
            // TODO enforce existence of entry point (main/start)
            return null;
        }

        private Token Expect(TokenKind kind)
        {
            if (tokens[pos].Kind != kind)
            {
                throw new ParseException(tokens[pos], $"expected {kind}, found {tokens[pos].Kind}");
            }
            return tokens[pos++];
        }

        private Subroutine ParseFunction(HashSet<string>? names = null)
        {
            Expect(TokenKind.FUNCTION);
            Token token = Expect(TokenKind.NAME);
            string name = token.Value?.ToString() ?? throw new ParseException(token, "function must have name");
            if (names == null)
            {
                if (subroutines.Any(f => f.Name == name))
                {
                    throw new ParseException(token, "top-level function names must be unique");
                }
            }
            else if (names.Contains(name))
            {
                throw new ParseException(token, "inner function names must be unique within that function");
            }
            Subroutine subroutine = new(name);
            Expect(TokenKind.NEWLINE);
            Expect(TokenKind.LEFT_CURLY);
            while (tokens[pos].Kind != TokenKind.RIGHT_CURLY)
            {
                if (tokens[pos].Kind == TokenKind.FUNCTION)
                {
                    Subroutine nested = ParseFunction(subroutine.statements
                        .Where(s => s is Subroutine)
                        .Cast<Subroutine>()
                        .Select(s => s.Name).ToHashSet());
                    subroutine.statements.Add(nested);
                }
                else if (tokens[pos].Kind == TokenKind.NAME)
                {
                    LabelStatement label = ParseLabel();
                    subroutine.statements.Add(label);
                }
                else if (tokens[pos].Kind == TokenKind.MNEMONIC)
                {
                    InstructionStatement instruction = ParseInstruction();
                    subroutine.statements.Add(instruction);
                }
                else if (tokens[pos].Kind == TokenKind.DATA)
                {
                    DataSegment data = ParseData();
                    subroutine.statements.Add(data);
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
            if (names == null)
            {
                subroutines.Add(subroutine);
            }
            return subroutine;
        }

        private LabelStatement ParseLabel()
        {
            Token token = Expect(TokenKind.NAME);
            string name = token.Value.ToString() ?? throw new ParseException(token, "label name is null");
            Expect(TokenKind.NEWLINE);
            if (labels.Any(label => label.Name == name))
            {
                throw new ParseException(token, "labels must be globally unique");
            }
            LabelStatement label = new(name);
            labels.Add(label);
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
                if (tokens[pos].Kind == TokenKind.NAME)
                {
                    instruction = new(tokens[pos]);
                }
                else if (tokens[pos].Kind == TokenKind.NUMBER)
                {
                    instruction = new(mnemonic, [ParseNumber(12)]);
                }
                else
                {
                    throw new ParseException(tokens[pos], $"expected name or address as argument of this instruction");
                }
                pos++;
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
            // TODO ensure uniqueness (globally? function-local? i think we need scopes...)
            DataSegment data = new(name);
            Expect(TokenKind.NEWLINE);
            Expect(TokenKind.LEFT_CURLY);
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

        private object Compile(object ast)
        {
            return null;
        }

        private void Emit(object code, string destPath)
        {

        }

        private readonly struct Token(int line, object value, TokenKind kind)
        {
            public int Line { get; } = line; // 1-based
            public object Value { get; } = value;
            public TokenKind Kind { get; } = kind;
        }

        private class ParseException(Token token, string message) :
            Exception($"line {token.Line}: '{token.Value}' - {message}")
        {

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

        private abstract class Statement
        {
        }

        private class DataSegment(string name) : Statement
        {
            public string Name { get; } = name;
            public readonly List<ushort> data = [];
        }

        private class LabelStatement(string name) : Statement
        {
            public string Name { get; } = name;
        }

        private class InstructionStatement : Statement
        {
            public readonly Instruction? instruction;
            public readonly Token? token;
            public readonly string? name;

            public InstructionStatement(string mnemonic, ushort[] args)
            {
                instruction = new Instruction(mnemonic, args);
                token = null;
                name = null;
            }

            public InstructionStatement(Token token)
            {
                instruction = null;
                this.token = token;
                name = token.Value.ToString();
            }
        }

        private class Subroutine(string name) : Statement
        {
            public string Name { get; } = name;
            public readonly List<Statement> statements = [];
        }
    }
}
