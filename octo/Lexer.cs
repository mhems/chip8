using static octo.Lexer;
using System.Text.RegularExpressions;
using System.Text;

namespace octo
{
    /*
     * program := statement+
     * directive := ':alias' NAME V_REGISTER
     *            | ':alias' NAME '{' constExpr '}'
     *            | ':' NAME
     *            | ':breakpoint' NAME
     *            | ':monitor' (NUMBER | v_register) (NUMBER | NAME | STRING)
     *            | ':call' constExpr
     *            | ':const' NAME (NUMBER | NAME)
     *            | ':unpack' (nibble | 'long') NAME
     *            | ':next' NAME statement
     *            | ':org' constExpr
     *            | ':macro' NAME NAME* '{' statement+ '}'
     *            | ':calc' NAME '{' constExpr '}'
     *            | ':byte' (NAME | NUMBER)
     *            | ':byte' '{' calcExpr '}'
     *            | ':pointer' (NAME | constExpr)
     *            | ':stringmode' NAME STRING '{' statement+ '}'
     *            | ':assert' STRING? '{' constExpr '}'
     * statement := 'return'
     *             | ';'
     *             | 'clear'
     *             | 'bcd' v_register
     *             | 'save' v_register
     *             | 'load' v_register
     *             | 'sprite' v_register v_register nibble
     *             | 'jump' (ADDRESS | NAME)
     *             | 'jump0' (ADDRESS | NAME)
     *             | assignment
     *             | controlStatement
     *             | NAME # function call
     *             | NAME NAME* # macro call
     *             | directive
     *             | NUMBER # data
     * assignment := 'delay' ':=' v_register
     *             | 'buzzer' ':=' v_register
     *             | 'i' ':=' (ADDRESS | NAME)
     *             | 'i' ':=' 'hex' v_register
     *             | 'i' '+=' v_register
     *             | v_register ':=' v_register
     *             | v_register '+=' v_register
     *             | v_register '-=' v_register
     *             | v_register ':=' (NUMBER | NAME)
     *             | v_register '+=' (NUMBER | NAME)
     *             | v_register '-=' (NUMBER | NAME)
     *             | v_register ':=' 'random' (NUMBER | NAME)
     *             | v_register ':=' 'delay'
     *             | v_register ':=' 'key'
     *             | v_register '=-' v_register
     *             | v_register '|=' v_register
     *             | v_register '&=' v_register
     *             | v_register '^=' v_register
     *             | v_register '>>=' v_register
     *             | v_register '<<=' v_register
     * conditional := v_register ('key' | '-key')
     *              | v_register ('<' | '<=' | '==' | '!=' | '>' | '>=') (NUMBER | NAME)
     *              | (NUMBER | NAME) ('<' | '<=' | '==' | '!=' | '>' | '>=') v_register
     *              | v_register ('<' | '<=' | '==' | '!=' | '>' | '>=') v_register
     * ifThen := 'if' conditional 'then' statement
     * ifBegin := 'if' conditional 'begin' statement+ 'end'
     * ifElse := 'if' conditional 'begin' statement+ 'else' statement+ 'end'
     * infiniteLoop := 'loop' statement+ danglingIf? 'again'
     * whileLoop := 'while' conditional statement
     * danglingIf := 'if' conditional 'then'
     * controlStatement := ifThen
     *                   | ifBegin
     *                   | ifElse
     *                   | infiniteLoop
     *                   | whileLoop
     * constExpr := '-' constExpr
     *            | '~' constExpr
     *            | '!' constExpr
     *            | '@' constExpr
     *            | 'sin' constExpr
     *            | 'cos' constExpr
     *            | 'tan' constExpr
     *            | 'exp' constExpr
     *            | 'log' constExpr
     *            | 'abs' constExpr
     *            | 'sqrt' constExpr
     *            | 'sign' constExpr
     *            | 'ceil' constExpr
     *            | 'floor' constExpr
     *            | 'strlen' constExpr
     *            | terminal '+' constExpr
     *            | terminal '-' constExpr
     *            | terminal '*' constExpr
     *            | terminal '/' constExpr
     *            | terminal '%' constExpr
     *            | terminal '&' constExpr
     *            | terminal '|' constExpr
     *            | terminal '^' constExpr
     *            | terminal '<<' constExpr
     *            | terminal '>>' constExpr
     *            | terminal '<' constExpr
     *            | terminal '<=' constExpr
     *            | terminal '>' constExpr
     *            | terminal '>=' constExpr
     *            | terminal '==' constExpr
     *            | terminal '!=' constExpr
     *            | terminal 'min' constExpr
     *            | terminal 'max' constExpr
     *            | terminal 'pow' constExpr
     *            | terminal
     * terminal := NUMBER | NAME | '(' constExpr ')'
     * v_register := V_REGISTER | NAME
     * V_REGISTER := 'v[0-9a-fA-F]'
     * NAME := [a-zA-Z0-9-_]+
     * ADDRESS := NUMBER
     * NUMBER := 0b[01]+ | 0x[0-9a-fA-F]+ | [1-9][0-9]*
     * STRING_CONSTANT := 'CHAR' | 'INDEX' | 'VALUE'
     * MACRO_CONSTANT := 'CALLS'
     */

    /*
     * superChipStatement := 'hires'
     *                     | 'lores'
     *                     | 'exit'
     *                     | 'scroll-down' (NUMBER | NAME)
     *                     | 'scroll-left' (NUMBER | NAME)
     *                     | 'scroll-right' (NUMBER | NAME)
     *                     | 'saveflags' v_register
     *                     | 'loadflags' v_register
     * superChipAssignment := 'i' ':=' 'bighex' v_register
     */

    /*
     * xoChipStatement := 'save' v_register '-' v_register
     *                  | 'load' v_register '-' v_register
     *                  | 'plane' (NUMBER | NAME)
     *                  | 'audio'
     *                  | 'scroll-up' (NUMBER | NAME)
     * xoChipAssignment := 'i' ':=' 'long' (NUMBER | NAME)
     *                   | 'pitch' ':=' v_register
     */

    public class Lexer
    {
        public const string RegisterLiteralRegex = "^(v[0-9a-fA-F]|i|delay|buzzer)$";
        public const string VRegisterLiteralRegex = "^v[0-9a-fA-F]$";
        public const string StringLiteralRegex = "^\"([^\"]|(\\[tnrv0\\]))*\"$";
        public const string DecimalNumberLiteralRegex = "^-?(0|([1-9][0-9]*))$";
        public const string BinaryNumberLiteralRegex = "^0b[01]+$";
        public const string HexNumberLiteralRegex = "^0x[0-9a-fA-F]+$";
        public const string NameRegex = "^[a-z0-9_-]+$";

        public static readonly HashSet<string> Keywords = [
            "return",
            "clear",
            "bcd",
            "save",
            "load",
            "sprite",
            "jump",
            "jump0",
            "hex",
            "random",
            "key",
            "-key",
            "if",
            "then",
            "begin",
            "end",
            "else",
            "loop",
            "again",
            "while",
            "long"
            ];

        public static readonly HashSet<string> Directives = [
            "call",
            "alias",
            "assert",
            "const",
            "unpack",
            "next",
            "macro",
            "org",
            "calc",
            "byte",
            "stringmode",
            "pointer",
            "breakpoint",
            "monitor"
            ];

        public static readonly HashSet<string> Operators = [
            ":",
            ":=",
            "+=",
            "-=",
            "=-",
            "|=",
            "&=",
            "^=",
            ">>=",
            "<<=",
            "==",
            "!=",
            "<",
            "<=",
            ">",
            ">=",
            ";"
            ];

        public static readonly Dictionary<string, int> Constants = new (){
            { "CALLS", 0},
            { "OCTO_KEY_1", 0x1},
            { "OCTO_KEY_2", 0x2},
            { "OCTO_KEY_3", 0x3},
            { "OCTO_KEY_4", 0xc},
            { "OCTO_KEY_Q", 0x4},
            { "OCTO_KEY_W", 0x5},
            { "OCTO_KEY_E", 0x6},
            { "OCTO_KEY_R", 0xd},
            { "OCTO_KEY_A", 0x7},
            { "OCTO_KEY_S", 0x8},
            { "OCTO_KEY_D", 0x9},
            { "OCTO_KEY_F", 0xe},
            { "OCTO_KEY_Z", 0xa},
            { "OCTO_KEY_X", 0x0},
            { "OCTO_KEY_C", 0xb},
            { "OCTO_KEY_V", 0xf},
            { "CHAR", 0},
            { "INDEX", 0},
            { "VALUE", 0},
        };

        public static readonly HashSet<string> CalcUnaryOperators = [
            "-",
            "~",
            "!",
            "sin",
            "cos",
            "tan",
            "exp",
            "log",
            "abs",
            "sqrt",
            "sign",
            "ceil",
            "floor",
            "@",
            "strlen",
            ];

        public static readonly HashSet<string> CalcBinaryOperators = [
            "-",
            "+",
            "*",
            "/",
            "%",
            "&",
            "|",
            "^",
            "<<",
            ">>",
            "pow",
            "min",
            "max",
            "<",
            "<=",
            "==",
            "!=",
            ">=",
            ">"
            ];

        public static readonly HashSet<string> CalcKeywords = [
            "E",
            "PI",
            "HERE"
            ];

        public enum TokenKind
        {
            UNKNOWN,
            NEWLINE,
            KEYWORD,
            NUMBER,
            NAME,
            DIRECTIVE,
            OPERATOR,
            REGISTER,
            CONSTANT,
            LEFT_CURLY_BRACE,
            RIGHT_CURLY_BRACE,
            LEFT_PAREN,
            RIGHT_PAREN,
            STRING,
        }

        public readonly struct Token(uint line, object value, TokenKind kind)
        {
            public uint Line { get; } = line; // 1-based
            public object Value { get; } = value;
            public TokenKind Kind { get; } = kind;

            public override string ToString()
            {
                return $"line {Line} '{Value}' ({Kind})";
            }
        }

        public static List<Token> Lex(string sourceFilePath)
        {
            List<Token> tokens = [];
            using (var reader = new StreamReader(sourceFilePath))
            {
                uint lineno = 1;
                while (!reader.EndOfStream)
                {
                    string? line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    int commentStart = line.IndexOf('#');
                    if (commentStart >= 0)
                    {
                        line = line[..commentStart];
                    }
                    line = line.Replace('\t', ' ');
                    foreach (string token in TokenizeLine(line))
                    {
                        tokens.Add(ClassifyToken(token, lineno));
                    }

                    tokens.Add(new Token(lineno, string.Empty, TokenKind.NEWLINE));
                    lineno++;
                }
            }

            return CoalesceNewlines(tokens);
        }

        private static List<string> TokenizeLine(string line)
        {
            string[] splits = line.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            List<string> coalesced = [];
            int i = 0;
            while (i < splits.Length)
            {
                if (splits[i].StartsWith('"'))
                {
                    StringBuilder acc = new();

                    while (i < splits.Length && !splits[i].EndsWith('"'))
                    {
                        acc.Append(' ');
                        acc.Append(splits[i]);
                        i++;
                    }

                    if (i < splits.Length)
                    {
                        acc.Append(' ');
                        acc.Append(splits[i]);
                    }

                    coalesced.Add(acc.ToString().Trim());
                }
                else
                {
                    coalesced.Add(splits[i]);
                }
                i++;
            }

            return coalesced;
        }

        private static Token ClassifyToken(string token, uint lineno)
        {
            if (token == "{")
            {
                return new Token(lineno, token, TokenKind.LEFT_CURLY_BRACE);
            }
            else if (token == "}")
            {
                return new Token(lineno, token, TokenKind.RIGHT_CURLY_BRACE);
            }
            else if (token == "(")
            {
                return new Token(lineno, token, TokenKind.LEFT_PAREN);
            }
            else if (token == ")")
            {
                return new Token(lineno, token, TokenKind.RIGHT_PAREN);
            }
            else if (Keywords.Contains(token) || CalcKeywords.Contains(token))
            {
                return new Token(lineno, token, TokenKind.KEYWORD);
            }
            else if (Constants.ContainsKey(token) || CalcKeywords.Contains(token))
            {
                return new Token(lineno, token, TokenKind.CONSTANT);
            }
            else if (TryParseNumber(token, out int number))
            {
                return new Token(lineno, number, TokenKind.NUMBER);
            }
            else if (Regex.IsMatch(token, StringLiteralRegex, RegexOptions.IgnoreCase))
            {
                return new Token(lineno, token, TokenKind.STRING);
            }
            else if (Regex.IsMatch(token, RegisterLiteralRegex))
            {
                return new Token(lineno, token, TokenKind.REGISTER);
            }
            else if (Regex.IsMatch(token, NameRegex, RegexOptions.IgnoreCase))
            {
                return new Token(lineno, token, TokenKind.NAME);
            }
            else if (Operators.Contains(token) ||
                     CalcUnaryOperators.Contains(token) ||
                     CalcBinaryOperators.Contains(token))
            {
                return new Token(lineno, token, TokenKind.OPERATOR);
            }
            else if (token.StartsWith(':'))
            {
                string remainder = token[1..];
                if (Directives.Contains(remainder))
                {
                    return new Token(lineno, token, TokenKind.DIRECTIVE);
                }
                else
                {
                    throw new LexException(lineno, $"unknown directive ':{remainder}'");
                }
            }
            else
            {
                throw new LexException(lineno, $"unknown token '{token}'");
            }
        }

        public static bool TryParseNumber(string token, out int number)
        {
            if (Regex.IsMatch(token, HexNumberLiteralRegex))
            {
                number = Convert.ToUInt16(token[2..], 16);
            }
            else if (Regex.IsMatch(token, BinaryNumberLiteralRegex))
            {
                number = Convert.ToUInt16(token[2..], 2);
            }
            else if (Regex.IsMatch(token, DecimalNumberLiteralRegex))
            {
                number = Convert.ToInt32(token);
            }
            else
            {
                number = 0;
                return false;
            }
            return true;
        }

        private static List<Token> CoalesceNewlines(List<Token> tokens)
        {
            List<Token> coalescedTokens = [];
            int i = 0;
            while (i < tokens.Count)
            {
                coalescedTokens.Add(tokens[i]);
                if (tokens[i].Kind == TokenKind.NEWLINE)
                {
                    while (i + 1 < tokens.Count && tokens[i + 1].Kind == TokenKind.NEWLINE)
                    {
                        i++;
                    }
                }

                i++;
            }

            return coalescedTokens;
        }

        public class LexException(string message) : Exception(message)
        {
            public LexException(uint lineno, string message) :
                this($"line {lineno}: {message}")
            {

            }
        }
    }
}
