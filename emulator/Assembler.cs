using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace emulator
{
    public static class Assembler
    {
        /*
         * function := FUNC NAME NEWLINE '{' NEWLINE
         *           ( ( function | LABEL | instruction ) NEWLINE )+
         *           '}' NEWLINE
         * LABEL := NAME NEWLINE 
         * instruction := "CLR" NEWLINE
         *              | "JMPI" (ADDRESS | NAME) NEWLINE
         *              | "CALL" (ADDRESS | NAME) NEWLINE
         *              | "SEQI" REG ARG NEWLINE
         *              | "SNEI" REG ARG NEWLINE
         *              | "SEQ" REG REG NEWLINE
         *              | "SETI" REG ARG NEWLINE
         *              | "ADDI" REG ARG NEWLINE
         *              | ("SET" | "OR" | "AND" | "XOR" | "ADD") REG REG NEWLINE
         *              | ("SUB" | "RSH" | "RSUB" | "LSH" | "SNE") REG REG NEWLINE
         *              | ("STO | "JMPA") (ADDRESS | NAME) NEWLINE
         *              | "RAND" REG ARG NEWLINE
         *              | "DRAW" REG REG NIBBLE NEWLINE
         *              | ("SKEQ" | "SKNE" | "DCPY" | "INP") REG NEWLINE
         *              | ("DLY" | "SND" | "SAVE" | "LD") REG NEWLINE
         *              | ("ADDM" | "SPR" | "BCD") REG NEWLINE
         *              # RET omitted
         * REG := 'v' [0-9a-f]
         * NIBBLE := num < 0x10
         * ARG := num < 0x100
         * ADDRESS := num < 0x1000
         * NAME := [a-z][a-z0-9_]*
         * NEWLINE := '\n' | '\r' | '\r\n'
         * FUNC := 'func'
         */

        private static readonly HashSet<string> mnemonics = [
            "CLR", "JMPI", "CALL", "SEQI", "SNEI", "SEQ", "SETI", "ADDI", "SET", "OR", "AND", "XOR",
            "ADD", "SUB", "RSH", "RSUB", "LSH", "SNE", "STO", "JMPA", "RAND", "DRAW", "SKEQ", "SKNE",
            "DCPY", "INP", "DLY", "SND", "SAVE", "LD", "ADDM", "SPR", "BCD"];
        private static readonly HashSet<string> nonaryMnemonics = ["CLR"];
        private static readonly HashSet<string> labelableMnemonics = ["JMPI", "CALL", "STO", "JMPA"];
        private static readonly HashSet<string> unaryMnemonics = ["SKEQ", "SKNE", "DCPY", "INP",
            "DLY", "SND", "SAVE", "LD", "ADDM", "SPR", "BCD"];
        private static readonly HashSet<string> regRegMnemonics = ["SEQ", "SET", "OR", "AND", "XOR", "ADD",
            "SUB", "RSH", "RSUB", "LSH", "SNE"];
        private static readonly HashSet<string> regArgMnemonics = ["SEQI", "SNEI", "SETI", "ADDI", "RAND"];
        private const string functionLiteralRegex = "func";
        private const string nameLiteralRegex = "[a-z][a-z0-9_]+";
        private const string decimalNumberLiteralRegex = "[1-9][0-9]*";
        private const string binaryNumberLiteralRegex = "0b[01]+";
        private const string hexNumberLiteralRegex = "0x[0-9a-f]+";
        private const string registerLiteralRegex = "v[0-9a-fA-F]";
        private const string entryPointLiteralRegex = "start|main";

        /*
         * label start/main says put jump at 0x200 to there
         * comments '#' are ignored to end of line
         * accept decimal, hex (0x) and bin (0b) literals
         * case-insensitive
         * names must be unique
         */
        public static void Assemble(string srcPath, string destPath)
        {
            List<Token> tokens = Lex(srcPath);
            var ast = Parse(tokens);
            var code = Compile(ast);
            Emit(code, destPath);
        }

        private static List<Token> Lex(string filepath)
        {
            List<Token> tokenStream = [];

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
                    string[] tokens = line.Replace('\t', ' ').Split(' ');
                    foreach (string token in tokens)
                    {
                        if (Regex.IsMatch(token, functionLiteralRegex, RegexOptions.IgnoreCase))
                        {
                            tokenStream.Add(new Token(lineno, token, TokenKind.FUNCTION));
                        }
                        else if (token == "{")
                        {
                            tokenStream.Add(new Token(lineno, token, TokenKind.LEFT_CURLY));
                        }
                        else if (token == "}")
                        {
                            tokenStream.Add(new Token(lineno, token, TokenKind.RIGHT_CURLY));
                        }
                        else if (mnemonics.Contains(token.ToUpper()))
                        {
                            tokenStream.Add(new Token(lineno, token.ToUpper(), TokenKind.MNEMONIC));
                        }
                        else if (Regex.IsMatch(token, registerLiteralRegex))
                        {
                            tokenStream.Add(new Token(lineno, token, TokenKind.REGISTER));
                        }
                        else if (Regex.IsMatch(token, nameLiteralRegex, RegexOptions.IgnoreCase))
                        {
                            tokenStream.Add(new Token(lineno, token, TokenKind.NAME));
                        }
                        else if (Regex.IsMatch(token, binaryNumberLiteralRegex, RegexOptions.IgnoreCase))
                        {
                            tokenStream.Add(new Token(lineno, token, TokenKind.BINARY_NUMBER));
                        }
                        else if (Regex.IsMatch(token, decimalNumberLiteralRegex, RegexOptions.IgnoreCase))
                        {
                            tokenStream.Add(new Token(lineno, token, TokenKind.DECIMAL_NUMBER));
                        }
                        else if (Regex.IsMatch(token, hexNumberLiteralRegex, RegexOptions.IgnoreCase))
                        {
                            tokenStream.Add(new Token(lineno, token, TokenKind.HEX_NUMBER));
                        }
                        else
                        {
                            throw new Exception($"line {lineno}: unknown token '{token}'");
                        }
                    }

                    tokenStream.Add(new Token(lineno, string.Empty, TokenKind.NEWLINE));
                    lineno++;
                }
            }

            return tokenStream;
        }

        private static object Parse(List<Token> tokens)
        {
            return null;
        }

        private static object Compile(object ast)
        {
            return null;
        }

        private static void Emit(object code, string destPath)
        {

        }

        private readonly struct Token(int line, string value, TokenKind kind)
        {
            public int Line { get; } = line; // 1-based
            public string Value { get; } = value;
            public TokenKind Kind { get; } = kind;
        }

        private enum TokenKind
        {
            UNKNOWN,
            LEFT_CURLY,
            RIGHT_CURLY,
            NEWLINE,
            NAME,
            REGISTER,
            BINARY_NUMBER,
            DECIMAL_NUMBER,
            HEX_NUMBER,
            FUNCTION,
            MNEMONIC,
        }
    }
}
