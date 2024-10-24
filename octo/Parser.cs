using static octo.Lexer;
using System.Text.RegularExpressions;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;

namespace octo
{

    public class Parser(List<Token> tokens)
    {
        private readonly List<Token> tokens = tokens;
        private int pos = 0;

        public Statement[] Parse()
        {
            List<Statement> statements = [];
            while (pos < tokens.Count)
            {
                while (pos < tokens.Count && tokens[pos].Kind == TokenKind.NEWLINE)
                {
                    pos++;
                }

                if (pos < tokens.Count)
                {
                    statements.Add(ParseStatement());
                }
            }
            return statements.ToArray();
        }

        private Statement ParseStatement(bool allowDanglingIf = false)
        {
            Token first = tokens[pos];
            string text = tokens[pos].Value.ToString();
            if (text.StartsWith(':'))
            {
                return ParseDirective();
            }

            if (tokens[pos].Kind == TokenKind.NAME ||
                tokens[pos].Kind == TokenKind.REGISTER)
            {
                string name = tokens[pos].Value.ToString();
                if (pos + 1 < tokens.Count)
                {
                    string next = tokens[pos + 1].Value.ToString();
                    if (next.Length > 1 && next[^1] == '=')
                    {
                        if (next != "<=" && next != ">=")
                        {
                            return ParseAssignment();
                        }
                        throw new ParseException(tokens[pos + 1], "illegal operator for assignment");
                    }
                    List<string> args = [];
                    pos++;
                    while (pos < tokens.Count && tokens[pos].Kind != TokenKind.NEWLINE)
                    {
                        args.Add(tokens[pos++].Value.ToString());
                    }
                    Expect(TokenKind.NEWLINE);
                    if (args.Count == 0)
                    {
                        return new AmbiguousCall(first, name);
                    }
                    return new MacroInvocation(first, name, args.ToArray());
                }
                pos++;
                return ExpectNewlineAndReturn(new AmbiguousCall(first, name));

            }
            else if (tokens[pos].Kind == TokenKind.REGISTER && text.StartsWith('v'))
            {
                return ParseAssignment();
            }

            if (tokens[pos].Kind == TokenKind.NUMBER)
            {
                return new DataDeclaration(tokens[pos], Convert.ToByte(tokens[pos++].Value));
            }

            pos++;
            switch (text)
            {
                case ";":
                case "return":
                    return ExpectNewlineAndReturn(new ReturnStatement(first));
                case "clear":
                    return ExpectNewlineAndReturn(new ClearStatement(first));
                case "bcd":
                    return ExpectNewlineAndReturn(new BcdStatement(first, ParseRegisterReference()));
                case "save":
                    return ExpectNewlineAndReturn(new SaveStatement(first, ParseRegisterReference()));
                case "load":
                    return ExpectNewlineAndReturn(new LoadStatement(first, ParseRegisterReference()));
                case "sprite":
                    GenericRegisterReference x = ParseRegisterReference();
                    GenericRegisterReference y = ParseRegisterReference();
                    RValue height;
                    if (tokens[pos].Kind == TokenKind.NUMBER)
                    {
                        height = ParseNumber();
                    }
                    else if (tokens[pos].Kind == TokenKind.NAME ||
                        tokens[pos].Kind == TokenKind.CONSTANT)
                    {
                        height = ParseName();
                    }
                    else
                    {
                        throw new ParseException(tokens[pos], "expected number or name of number");
                    }
                    return ExpectNewlineAndReturn(new SpriteStatement(first, x, y, height));
                case "jump":
                    if (tokens[pos].Kind == TokenKind.NAME)
                    {
                        return ExpectNewlineAndReturn(new JumpImmediateStatement(first, ParseName()));
                    }
                    return ExpectNewlineAndReturn(new JumpImmediateStatement(first, ParseNumber()));
                case "jump0":
                    if (tokens[pos].Kind == TokenKind.NAME)
                    {
                        return ExpectNewlineAndReturn(new JumpAdditiveStatement(first, ParseName()));
                    }
                    return ExpectNewlineAndReturn(new JumpAdditiveStatement(first, ParseNumber()));
                case "if":
                    pos--;
                    return ParseIf(allowDanglingIf);
                case "loop":
                    pos--;
                    return ParseLoop();
                case "while":
                    pos--;
                    return ParseWhileStatement();
            }
            throw new ParseException(tokens[pos], "expected statement");
        }

        private Assignment ParseAssignment()
        {
            string text = tokens[pos].Value.ToString();
            Token first = tokens[pos];
            switch (text)
            {
                case "delay":
                    pos++;
                    if (tokens[pos].Value.ToString() != ":=")
                    {
                        throw new ParseException(tokens[pos], "delay can only be assigned to");
                    }
                    pos++;
                    return ExpectNewlineAndReturn(new LoadDelayAssignment(first, ParseRegisterReference()));
                case "buzzer":
                    pos++;
                    if (tokens[pos].Value.ToString() != ":=")
                    {
                        throw new ParseException(tokens[pos], "buzzer can only be assigned to");
                    }
                    pos++;
                    return ExpectNewlineAndReturn(new LoadBuzzerAssignment(first, ParseRegisterReference()));
                case "i":
                    pos++;
                    switch (tokens[pos].Value.ToString())
                    {
                        case ":=":
                            pos++;
                            if (tokens[pos].Value.ToString() == "hex")
                            {
                                pos++;
                                return ExpectNewlineAndReturn(new LoadCharacterAssignment(first, ParseRegisterReference()));
                            }
                            else if (tokens[pos].Kind == TokenKind.NUMBER)
                            {
                                return ExpectNewlineAndReturn(new MemoryAssignment(first, ParseNumber()));
                            }
                            return ExpectNewlineAndReturn(new MemoryAssignment(first, ParseName()));
                        case "+=":
                            pos++;
                            return ExpectNewlineAndReturn(new MemoryIncrementAssignment(first, ParseRegisterReference()));
                        default:
                            throw new ParseException(tokens[pos], "expected operator ':=' or '+=' when assigning to 'i'");
                    }
            }

            GenericRegisterReference src;
            GenericRegisterReference dest = ParseRegisterReference();
            Token opToken = Expect(TokenKind.OPERATOR);
            switch (opToken.Value.ToString())
            {
                case ":=":
                    switch (tokens[pos].Value.ToString())
                    {
                        case "key":
                            pos++;
                            return ExpectNewlineAndReturn(new KeyInputAssignment(first, dest));
                        case "delay":
                            pos++;
                            return ExpectNewlineAndReturn(new SaveDelayAssignment(first, dest));
                        case "random":
                            pos++;
                            if (tokens[pos].Kind == TokenKind.NUMBER)
                            {
                                RValue mask = ParseNumber();
                                return new RandomAssignment(first, dest, mask);
                            }
                            return ExpectNewlineAndReturn(new RandomAssignment(first, dest, ParseName()));
                    }

                    if (tokens[pos].Kind == TokenKind.NUMBER)
                    {
                        return ExpectNewlineAndReturn(new ImmediateAssignment(first, dest, ParseNumber()));
                    }
                    else if (tokens[pos].Kind == TokenKind.REGISTER)
                    {
                        return ExpectNewlineAndReturn(new RegisterCopyAssignment(first, dest, ParseRegisterReference()));
                    }
                    else
                    {
                        return ExpectNewlineAndReturn(new ImmediateAssignment(first, dest, ParseName()));
                    }
                case "+=":
                    if (tokens[pos].Kind == TokenKind.NUMBER)
                    {
                        return ExpectNewlineAndReturn(new ImmediateAdditionAssignment(first, dest, ParseNumber()));
                    }
                    else if (tokens[pos].Kind == TokenKind.REGISTER)
                    {
                        return ExpectNewlineAndReturn(new RegisterAddAssignment(first, dest, ParseRegisterReference()));
                    }
                    else
                    {
                        return ExpectNewlineAndReturn(new ImmediateAdditionAssignment(first, dest, ParseName()));
                    }
                case "-=":
                    if (tokens[pos].Kind == TokenKind.NUMBER)
                    {
                        return ExpectNewlineAndReturn(new ImmediateSubtractionAssignment(first, dest, ParseNumber()));
                    }
                    else if (tokens[pos].Kind == TokenKind.REGISTER)
                    {
                        return ExpectNewlineAndReturn(new RegisterSubtractionAssignment(first, dest, ParseRegisterReference()));
                    }
                    else
                    {
                        return ExpectNewlineAndReturn(new ImmediateSubtractionAssignment(first, dest, ParseName()));
                    }
                case "=-":
                    src = ParseRegisterReference();
                    return ExpectNewlineAndReturn(new RegisterReverseSubtractionAssignment(first, dest, src));
                case "|=":
                    src = ParseRegisterReference();
                    return ExpectNewlineAndReturn(new RegisterOrAssignment(first, dest, src));
                case "&=":
                    src = ParseRegisterReference();
                    return ExpectNewlineAndReturn(new RegisterAndAssignment(first, dest, src));
                case "^=":
                    src = ParseRegisterReference();
                    return ExpectNewlineAndReturn(new RegisterXorAssignment(first, dest, src));
                case ">>=":
                    src = ParseRegisterReference();
                    return ExpectNewlineAndReturn(new RegisterRightShiftAssignment(first, dest, src));
                case "<<=":
                    src = ParseRegisterReference();
                    return ExpectNewlineAndReturn(new RegisterLeftShiftAssignment(first, dest, src));
                default:
                    throw new ParseException(tokens[pos], "unknown operator");
            }
        }

        #region directives
        private Directive ParseDirective()
        {
            Directive directive = tokens[pos].Value.ToString() switch
            {
                ":alias" => ParseAliasDirective(),
                ":" => ParseLabelDeclaration(),
                ":call" => ParseFunctionCallDirective(),
                ":const" => ParseConstantDirective(),
                ":unpack" => ParseUnpackDirective(),
                ":next" => ParseNextDirective(),
                ":org" => ParseOrgDirective(),
                ":macro" => ParseMacroDefinition(),
                ":calc" => ParseCalculationDirective(),
                ":byte" => ParseByteDirective(),
                ":pointer" => ParsePointerDirective(),
                ":stringmode" => ParseStringmodeDirective(),
                ":assert" => ParseAssertDirective(),
                ":breakpoint" => ParseBreakpointDirective(),
                ":monitor" => ParseMonitorDirective(),
                _ => throw new ParseException(tokens[pos], "no directive found")
            };
            Expect(TokenKind.NEWLINE);
            return directive;
        }

        private DebuggingDirective ParseBreakpointDirective()
        {
            Token t = ExpectDirective("breakpoint");
            string name = Expect(TokenKind.NAME).Value.ToString();
            return new DebuggingDirective(t, ":breakpoint " + name);
        }

        private DebuggingDirective ParseMonitorDirective()
        {
            Token t = ExpectDirective("monitor");
            if (tokens[pos].Kind == TokenKind.NUMBER)
            {
                ParseNumber();
            }
            else
            {
                ParseRegisterReference();
            }
            switch (tokens[pos].Kind)
            {
                case TokenKind.STRING:
                case TokenKind.NUMBER:
                case TokenKind.NAME:
                case TokenKind.CONSTANT:
                    pos++;
                    break;
                default:
                    throw new ParseException(tokens[pos], "monitor directive must end with number, name, or string");
            }
            return new DebuggingDirective(t, ":monitor " + string.Join(' ', tokens[(pos-2)..pos].Select(t => t.Value.ToString())));
        }

        private Alias ParseAliasDirective()
        {
            Token t = ExpectDirective("alias");
            string name = ParseName().Name;
            if (tokens[pos].Kind == TokenKind.LEFT_CURLY_BRACE)
            {
                Expect(TokenKind.LEFT_CURLY_BRACE);
                CalculationExpression e = ParseCalculationExpression();
                Expect(TokenKind.RIGHT_CURLY_BRACE);
                return new ConstantAlias(t, name, e);
            }
            if (tokens[pos].Kind == TokenKind.REGISTER)
            {
                if (tokens[pos].Value != null && tokens[pos].Value.ToString().StartsWith('v'))
                {
                    byte index = Convert.ToByte(tokens[pos].Value.ToString()[1..], 16);
                    pos++;
                    return new RegisterAlias(t, name, index);
                }
                throw new ParseException(tokens[pos], "can only alias 'v' registers");
            }
            throw new ParseException(tokens[pos], "illegal alias target");
        }
        
        private LabelDeclaration ParseLabelDeclaration()
        {
            Token t = tokens[pos];
            if (tokens[pos].Kind == TokenKind.OPERATOR &&
                tokens[pos].Value.ToString() == ":")
            {
                pos++;
                if (tokens[pos].Kind == TokenKind.NEWLINE)
                {
                    pos++;
                }
                return new LabelDeclaration(t, ParseName().Name);
            }
            throw new ParseException("expected a label declaration");
        }

        private FunctionCall ParseFunctionCallDirective()
        {
            Token t = ExpectDirective("call");
            CalculationExpression expr = ParseCalculationExpression();
            return new FunctionCall(t, expr);
        }

        private ConstantDirective ParseConstantDirective()
        {
            Token t = ExpectDirective("const");
            string name = ParseName().Name;
            if (tokens[pos].Kind == TokenKind.NAME)
            {
                return new ConstantDirective(t, name, ParseName());
            }
            else if (tokens[pos].Kind == TokenKind.NUMBER)
            {
                return new ConstantDirective(t, name, ParseNumber());
            }
            throw new ParseException(tokens[pos], "expected name or number to follow const directive");
        }

        private UnpackDirective ParseUnpackDirective()
        {
            Token t = ExpectDirective("unpack");
            if (tokens[pos].Kind == TokenKind.NUMBER)
            {
                NumericLiteral num = ParseNumber();
                NamedReference name = ParseName();
                return new UnpackNumberDirective(t, name, num);
            }
            else
            {
                ExpectKeyword("long");
                NamedReference name = ParseName();
                return new UnpackLongDirective(t, name);
            }
        }

        private NextDirective ParseNextDirective()
        {
            Token t = ExpectDirective("next");
            string name = ParseName().Name;
            Statement stmt = ParseStatement();
            return new NextDirective(t, name, stmt);
        }

        private OrgDirective ParseOrgDirective()
        {
            Token t = ExpectDirective("org");
            CalculationExpression expr = ParseCalculationExpression();
            return new OrgDirective(t, expr);
        }

        private MacroDefinition ParseMacroDefinition()
        {
            Token t = ExpectDirective("macro");
            string name = ParseName().Name;
            List<string> parameters = [];
            while (tokens[pos].Kind != TokenKind.LEFT_CURLY_BRACE &&
                tokens[pos].Kind != TokenKind.NEWLINE)
            {
                parameters.Add(ParseName().Name);
            }
            if (tokens[pos].Kind == TokenKind.NEWLINE)
            {
                pos++;
            }
            Expect(TokenKind.LEFT_CURLY_BRACE);
            if (tokens[pos].Kind == TokenKind.NEWLINE)
            {
                pos++;
            }
            List<Statement> body = [];
            while (tokens[pos].Kind != TokenKind.RIGHT_CURLY_BRACE)
            {
                body.Add(ParseStatement());
            }
            Expect(TokenKind.RIGHT_CURLY_BRACE);
            return new MacroDefinition(t, name, parameters.ToArray(), body.ToArray());
        }

        private Calculation ParseCalculationDirective()
        {
            Token t = ExpectDirective("calc");
            string name = ParseName().Name;
            Expect(TokenKind.LEFT_CURLY_BRACE);
            CalculationExpression expr = ParseCalculationExpression();
            Expect(TokenKind.RIGHT_CURLY_BRACE);
            return new Calculation(t, name, expr);
        }

        private ByteDirective ParseByteDirective()
        {
            Token t = ExpectDirective("byte");
            switch (tokens[pos].Kind)
            {
                case TokenKind.LEFT_CURLY_BRACE:
                    Expect(TokenKind.LEFT_CURLY_BRACE);
                    CalculationExpression expr = ParseCalculationExpression();
                    Expect(TokenKind.RIGHT_CURLY_BRACE);
                    return new ExpressionByteDirective(t, expr);
                case TokenKind.NAME:
                    NamedReference name = ParseName();
                    return new ValueByteDirective(t, name);
                 case TokenKind.NUMBER:
                    NumericLiteral num = ParseNumber();
                    return new ValueByteDirective(t, num);
                default:
                    throw new ParseException(tokens[pos], "expected constant expression, name, or number to follow :byte directive");
            }
        }
        
        private PointerDirective ParsePointerDirective()
        {
            Token t = ExpectDirective("pointer");
            CalculationExpression expr = ParseCalculationExpression();
            return new PointerDirective(t, expr);
        }

        private StringDirective ParseStringmodeDirective()
        {
            Token t = ExpectDirective("stringmode");
            string name = ParseName().Name;
            string text = Expect(TokenKind.STRING).Value.ToString().Trim('"');
            if (tokens[pos].Kind == TokenKind.NEWLINE)
            {
                pos++;
            }
            Expect(TokenKind.LEFT_CURLY_BRACE);
            Expect(TokenKind.NEWLINE);
            List<Statement> body = [];
            while (tokens[pos].Kind != TokenKind.RIGHT_CURLY_BRACE)
            {
                body.Add(ParseStatement());
            }
            Expect(TokenKind.RIGHT_CURLY_BRACE);
            return new StringDirective(t, name, text, body.ToArray());
        }

        private AssertDirective ParseAssertDirective()
        {
            Token t = ExpectDirective("assert");
            string? message = null;
            if (tokens[pos].Kind == TokenKind.STRING)
            {
                message = Expect(TokenKind.STRING).Value.ToString().Trim('"');
                pos++;
            }
            if (tokens[pos].Kind == TokenKind.NEWLINE)
            {
                pos++;
            }
            Expect(TokenKind.LEFT_CURLY_BRACE);
            CalculationExpression expr = ParseCalculationExpression();
            Expect(TokenKind.RIGHT_CURLY_BRACE);
            return new AssertDirective(t, expr, message);
        }
        #endregion

        #region calculations
        private CalculationExpression ParseCalculationExpression()
        {
            Token firstToken = tokens[pos];
            string first = tokens[pos].Value.ToString();
            if (Lexer.CalcUnaryOperators.Contains(first))
            {
                if (first == "strlen")
                {
                    string name;
                    bool text;
                    if (tokens[pos].Kind == TokenKind.STRING)
                    {
                        name = Expect(TokenKind.STRING).Value.ToString().Trim('"');
                        text = true;
                    }
                    else if (tokens[pos].Kind == TokenKind.NAME ||
                        tokens[pos].Kind == TokenKind.CONSTANT)
                    {
                        name = tokens[pos].Value.ToString();
                        text = false;
                        pos++;
                    }
                    else
                    {
                        throw new ParseException(tokens[pos], "expect name or string after strlen operator");
                    }
                    return new CalculationStringLengthOperation(firstToken, name, text);
                }
                CalculationUnaryOperation.UnaryOperator op = first switch
                {
                    "-" => CalculationUnaryOperation.UnaryOperator.NumericalNegation,
                    "~" => CalculationUnaryOperation.UnaryOperator.BitwiseNegation,
                    "!" => CalculationUnaryOperation.UnaryOperator.LogicalNegation,
                    "@" => CalculationUnaryOperation.UnaryOperator.AddressOf,
                    "sin" => CalculationUnaryOperation.UnaryOperator.Sine,
                    "cos" => CalculationUnaryOperation.UnaryOperator.Cosine,
                    "tan" => CalculationUnaryOperation.UnaryOperator.Tangent,
                    "exp" => CalculationUnaryOperation.UnaryOperator.Exponentiation,
                    "log" => CalculationUnaryOperation.UnaryOperator.Logarithm,
                    "abs" => CalculationUnaryOperation.UnaryOperator.AbsoluteValue,
                    "sqrt" => CalculationUnaryOperation.UnaryOperator.SquareRoot,
                    "sign" => CalculationUnaryOperation.UnaryOperator.Sign,
                    "ceil" => CalculationUnaryOperation.UnaryOperator.Ceiling,
                    "floor" => CalculationUnaryOperation.UnaryOperator.Floor,
                    _ => throw new ParseException(tokens[pos], "unknown unary operator")
                };
                pos++;
                CalculationExpression expr = ParseCalculationExpression();
                return new CalculationUnaryOperation(firstToken, expr, op);
            }
            CalculationExpression terminal = ParseCalculationTerminal();
            string follow = tokens[pos].Value.ToString();
            if (Lexer.CalcBinaryOperators.Contains(follow))
            {
                CalculationBinaryOperation.BinaryOperator op = follow switch
                {
                    "+" => CalculationBinaryOperation.BinaryOperator.Addition,
                    "-" => CalculationBinaryOperation.BinaryOperator.Subtraction,
                    "*" => CalculationBinaryOperation.BinaryOperator.Multiplication,
                    "/" => CalculationBinaryOperation.BinaryOperator.Division,
                    "%" => CalculationBinaryOperation.BinaryOperator.Modulus,
                    "&" => CalculationBinaryOperation.BinaryOperator.And,
                    "|" => CalculationBinaryOperation.BinaryOperator.Or,
                    "^" => CalculationBinaryOperation.BinaryOperator.Xor,
                    "<<" => CalculationBinaryOperation.BinaryOperator.LeftShift,
                    ">>" => CalculationBinaryOperation.BinaryOperator.RightShift,
                    "<" => CalculationBinaryOperation.BinaryOperator.LessThan,
                    "<=" => CalculationBinaryOperation.BinaryOperator.LessThanOrEqual,
                    ">" => CalculationBinaryOperation.BinaryOperator.GreaterThan,
                    ">=" => CalculationBinaryOperation.BinaryOperator.GreaterThanOrEqual,
                    "==" => CalculationBinaryOperation.BinaryOperator.EqualTo,
                    "!=" => CalculationBinaryOperation.BinaryOperator.NotEqualTo,
                    "min" => CalculationBinaryOperation.BinaryOperator.Minimum,
                    "max" => CalculationBinaryOperation.BinaryOperator.Maximum,
                    "pow" => CalculationBinaryOperation.BinaryOperator.Exponentiation,
                    _ => throw new ParseException(tokens[pos], "unknown binary operator"),
                };
                pos++;
                CalculationExpression expr = ParseCalculationExpression();
                return new CalculationBinaryOperation(firstToken, terminal, expr, op);
            }
            return terminal;
        }

        private CalculationExpression ParseCalculationTerminal()
        {
            Token first = tokens[pos];
            if (tokens[pos].Kind == TokenKind.LEFT_PAREN)
            {
                pos++;
                CalculationExpression expr = ParseCalculationExpression();
                Expect(TokenKind.RIGHT_PAREN);
                return expr;
            }
            if (tokens[pos].Kind == TokenKind.NUMBER)
            {
                pos++;
                return new CalculationNumber(first, Convert.ToDouble(tokens[pos-1].Value));
            }
            if (tokens[pos].Kind == TokenKind.NAME ||
                tokens[pos].Kind == TokenKind.CONSTANT)
            {
                pos++;
                return new CalculationName(first, tokens[pos-1].Value.ToString());
            }
            throw new ParseException(tokens[pos], "unable to find terminal token");
        }
        #endregion

        #region control flow statements
        private ConditionalExpression ParseCondition()
        {
            RValue left = ParseRValue();
            string opText = tokens[pos].Value.ToString();
            pos++;
            if (opText == "key" || opText == "-key")
            {
                return new KeystrokeCondition(left.FirstToken, left, opText == "key");
            }
            RValue right = ParseRValue();
            BinaryConditionalExpression.ConditionalOperator op = opText switch
            {
                "==" => BinaryConditionalExpression.ConditionalOperator.Equality,
                "!=" => BinaryConditionalExpression.ConditionalOperator.Inequality,
                "<"  => BinaryConditionalExpression.ConditionalOperator.LessThan,
                "<=" => BinaryConditionalExpression.ConditionalOperator.LessThanOrEqualTo,
                ">"  => BinaryConditionalExpression.ConditionalOperator.GreaterThan,
                ">=" => BinaryConditionalExpression.ConditionalOperator.GreaterThanOrEqualTo,
                _ => throw new ParseException(tokens[pos-2], $"unknown operator {opText}")
            };
            return new BinaryConditionalExpression(left.FirstToken, left, op, right);
        }

        private Statement ParseIf(bool allowDanglingIf)
        {
            Token t = ExpectKeyword("if");
            ConditionalExpression expr = ParseCondition();
            if (tokens[pos].Kind == TokenKind.NEWLINE)
            {
                pos++;
            }
            if (tokens[pos].Value.ToString() == "begin")
            {
                ExpectKeyword("begin");
                Expect(TokenKind.NEWLINE);
                (Statement[] thenBlock, string keyword) = ParseStatementsUntilKeyword(["end", "else"]);
                if (keyword == "end")
                {
                    Expect(TokenKind.NEWLINE);
                    return new IfElseBlock(t, expr, thenBlock, []);
                }
                Expect(TokenKind.NEWLINE);
                Statement[] elseBlock = ParseStatementsUntilKeyword("end");
                Expect(TokenKind.NEWLINE);
                return new IfElseBlock(t, expr, thenBlock, elseBlock);
            }
            else if (tokens[pos].Value.ToString() == "then")
            {
                ExpectKeyword("then");

                if ((tokens[pos].Kind == TokenKind.KEYWORD &&
                     tokens[pos].Value != null &&
                     tokens[pos].Value.ToString() == "again") ||
                    (tokens[pos].Kind == TokenKind.NEWLINE &&
                     tokens[pos+1].Kind == TokenKind.KEYWORD &&
                     tokens[pos+1].Value != null &&
                     tokens[pos+1].Value.ToString() == "again"))
                {
                    if (tokens[pos].Kind == TokenKind.NEWLINE)
                    {
                        pos++;
                    }
                    if (!allowDanglingIf)
                    {
                        throw new ParseException(tokens[pos], "dangling if not allowed in non-loop context");
                    }
                    return new IfStatement(t, expr);
                }

                Statement body = ParseStatement();
                return new IfStatement(t, expr, body);
            }
            throw new ParseException(tokens[pos], "expected keyword 'then' or 'begin' after 'if'");
        }

        private LoopStatement ParseLoop()
        {
            Token t = ExpectKeyword("loop");
            if (tokens[pos].Kind == TokenKind.KEYWORD && tokens[pos].Value.ToString() == "again")
            {
                pos++;
                return ExpectNewlineAndReturn(new LoopStatement(t, []));
            }
            Expect(TokenKind.NEWLINE);
            Statement[] body = ParseStatementsUntilKeyword("again");
            Expect(TokenKind.NEWLINE);
            return new LoopStatement(t, body);
        }

        private WhileStatement ParseWhileStatement()
        {
            Token t = ExpectKeyword("while");
            ConditionalExpression expr = ParseCondition();
            Statement body = ParseStatement();
            return new WhileStatement(t, expr, body);
        }
        #endregion

        private Statement[] ParseStatementsUntilKeyword(string keyword)
        {
            (Statement[] statements, string _) = ParseStatementsUntilKeyword([keyword]);
            return statements;
        }

        private (Statement[], string) ParseStatementsUntilKeyword(string[] keywordChoices)
        {
            List<Statement> block = [];
            string? keyword = null;
            bool allowDanglingIf = keywordChoices.Length == 1 && keywordChoices[0] == "again";
            while (pos < tokens.Count)
            {
                block.Add(ParseStatement(allowDanglingIf));

                if (tokens[pos].Kind == TokenKind.KEYWORD &&
                    tokens[pos].Value != null &&
                    keywordChoices.Contains(tokens[pos].Value.ToString()))
                {
                    keyword = tokens[pos].Value.ToString();
                    pos++;
                    break;
                }
            }
            if (keyword == null)
            {
                throw new ParseException(tokens[pos],
                    $"unable to find keyword '{string.Join(", ", keywordChoices)}'");
            }
            return ([.. block], keyword);
        }

        #region atoms
        private GenericRegisterReference ParseRegisterReference()
        {
            Token first = tokens[pos];
            string? text = tokens[pos].Value.ToString() ??
                throw new ParseException(tokens[pos], "null token");
            if (tokens[pos].Kind == TokenKind.REGISTER && text.StartsWith('v'))
            {
                pos++;
                return new GenericRegisterReference(first, Convert.ToByte(text[1..], 16));
            }
            else if (tokens[pos].Kind == TokenKind.NAME)
            {
                pos++;
                return new GenericRegisterReference(first, text);
            }
            throw new ParseException(tokens[pos], "expected register reference");
        }

        private NamedReference ParseName()
        {
            if (tokens[pos].Kind == TokenKind.NAME)
            {
                if (tokens[pos].Value != null)
                {
                    return new NamedReference(tokens[pos], tokens[pos++].Value.ToString());
                }
                throw new ParseException(tokens[pos], "token is null");
            }
            throw new ParseException(tokens[pos], "expected a name");
        }

        private NumericLiteral ParseNumber()
        {
            if (tokens[pos].Kind == TokenKind.NUMBER)
            {
                if (tokens[pos].Value != null)
                {
                    return new NumericLiteral(tokens[pos], Convert.ToUInt16(tokens[pos++].Value.ToString()));
                }
                throw new ParseException(tokens[pos], "token is null");
            }
            throw new ParseException(tokens[pos], "expected a number");
        }

        private RValue ParseRValue()
        {
            return (tokens[pos].Kind) switch
            {
                TokenKind.NUMBER => ParseNumber(),
                TokenKind.CONSTANT => ParseName(),
                TokenKind.NAME => ParseName(),
                TokenKind.REGISTER => ParseRegisterReference(),
                _ => throw new ParseException(tokens[pos], "expected r-value"),
            };
        }
        #endregion

        #region helpers
        private Token Expect(TokenKind kind)
        {
            if (tokens[pos].Kind != kind)
            {
                throw new ParseException(tokens[pos], $"expected {kind}, found {tokens[pos].Kind}");
            }
            return tokens[pos++];
        }

        private Token ExpectKeyword(string text)
        {
            Token token = Expect(TokenKind.KEYWORD);
            if (token.Value == null)
            {
                throw new ParseException(token, "null token");
            }
            if (token.Value.ToString() != text)
            {
                throw new ParseException(token, $"expected keyword '{text}'");
            }
            return token;
        }

        private Token ExpectDirective(string text)
        {
            Token token = Expect(TokenKind.DIRECTIVE);
            if (token.Value == null)
            {
                throw new ParseException(token, "null token");
            }
            text = ":" + text;
            if (token.Value.ToString() != text)
            {
                throw new ParseException(token, $"expected directive '{text}'");
            }
            return token;
        }

        private T ExpectNewlineAndReturn<T>(T t)
        {
            Expect(TokenKind.NEWLINE);
            return t;
        }
        #endregion

        public class ParseException(string message) : Exception(message)
        {
            public ParseException(uint lineno, string message) :
                this($"line {lineno}: {message}")
            {

            }

            public ParseException(Token token, string message) :
                this($"line {token.Line} '{token.Value}' - {message}")
            {

            }
        }
    }
}
