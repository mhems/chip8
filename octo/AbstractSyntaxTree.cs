using System.Security.Cryptography;
using System.Text;
using static octo.Lexer;

namespace octo
{
    public abstract class AstNode(Token token)
    {
        public Token FirstToken { get; } = token;
    }

    #region statements
    public abstract class Statement(Token token) : AstNode(token)
    {

    }

    public class ReturnStatement(Token token) : Statement(token)
    {
        public override string ToString()
        {
            return "return";
        }
    }

    public class ClearStatement(Token token) : Statement(token)
    {
        public override string ToString()
        {
            return "clear";
        }
    }

    public class BcdStatement(Token token, GenericRegisterReference reg) : Statement(token)
    {
        public GenericRegisterReference Register { get; private set; } = reg;

        public void Resolve(GenericRegisterReference reg)
        {
            Register = reg;
        }

        public override string ToString()
        {
            return $"bcd {Register}";
        }
    }

    public class SaveStatement(Token token, GenericRegisterReference reg) : Statement(token)
    {
        public GenericRegisterReference Register { get; private set; } = reg;

        public void Resolve(GenericRegisterReference reg)
        {
            Register = reg;
        }

        public override string ToString()
        {
            return $"save {Register}";
        }
    }

    public class LoadStatement(Token token, GenericRegisterReference reg) : Statement(token)
    {
        public GenericRegisterReference Register { get; private set; } = reg;

        public void Resolve(GenericRegisterReference reg)
        {
            Register = reg;
        }

        public override string ToString()
        {
            return $"load {Register}";
        }
    }

    public class SpriteStatement(
        Token token,
        GenericRegisterReference x,
        GenericRegisterReference y,
        RValue height) :
        Statement(token)
    {
        public GenericRegisterReference XRegister { get; private set; } = x;
        public GenericRegisterReference YRegister { get; private set; } = y;
        public RValue Height { get; private set; } = height;


        public void ResolveX(GenericRegisterReference reg)
        {
            XRegister = reg;
        }

        public void ResolveY(GenericRegisterReference reg)
        {
            YRegister = reg;
        }

        public void ResolveHeight(RValue height)
        {
            Height = height;
        }

        public override string ToString()
        {
            return $"sprite {XRegister} {YRegister} {Height}";
        }
    }

    public abstract class JumpStatement(Token token, bool immediate) : Statement(token)
    {
        public bool Immediate { get; } = immediate;
        public NamedReference? TargetName { get; }
        public NumericLiteral? TargetBaseAddress { get; }

        public JumpStatement(Token token, NamedReference target, bool immediate) : this(token, immediate) { TargetName = target; }
        public JumpStatement(Token token, NumericLiteral address, bool immediate) : this(token, immediate) { TargetBaseAddress = address; }

        public override string ToString()
        {
            return $"jump{(Immediate ? "" : "0")} {TargetName?.ToString() ?? TargetBaseAddress?.ToString() ?? "???"}";
        }
    }

    public class JumpImmediateStatement : JumpStatement
    {
        public JumpImmediateStatement(Token token, NamedReference target) : base(token, target, true) { }
        public JumpImmediateStatement(Token token, NumericLiteral address) : base(token, address, true) { }
    }

    public class JumpAdditiveStatement : JumpStatement
    {
        public JumpAdditiveStatement(Token token, NamedReference target) : base(token, target, false) { }
        public JumpAdditiveStatement(Token token, NumericLiteral address) : base(token, address, false) { }
    }

    public class MacroInvocation(Token token, string name, string[] args) : Statement(token)
    {
        public string MacroName { get; } = name;
        public string[] Arguments { get; } = args;

        public override string ToString()
        {
            return $"{MacroName} {string.Join(' ', args)}";
        }
    }

    public class AmbiguousCall(Token token, string name) : Statement(token)
    {
        public string TargetName { get; } = name;

        public override string ToString()
        {
            return TargetName;
        }
    }

    public class DataDeclaration(Token token, byte value) : Statement(token)
    {
        public byte Value { get; } = value;

        public override string ToString()
        {
            return $"0x{Value:x}";
        }
    }
    #endregion

    #region assignments
    public abstract class Assignment(Token token) : Statement(token)
    {

    }

    public class LoadDelayAssignment(Token token, GenericRegisterReference reg) : Assignment(token)
    {
        public GenericRegisterReference SourceRegister { get; private set; } = reg;

        public void Resolve(GenericRegisterReference reg)
        {
            SourceRegister = reg;
        }

        public override string ToString()
        {
            return $"delay := {SourceRegister}";
        }
    }

    public class SaveDelayAssignment(Token token, GenericRegisterReference reg) : Assignment(token)
    {
        public GenericRegisterReference DestinationRegister { get; private set; } = reg;

        public void Resolve(GenericRegisterReference reg)
        {
            DestinationRegister = reg;
        }

        public override string ToString()
        {
            return $"{DestinationRegister} := delay";
        }
    }

    public class LoadBuzzerAssignment(Token token, GenericRegisterReference reg) : Assignment(token)
    {
        public GenericRegisterReference SourceRegister { get; private set; } = reg;

        public void Resolve(GenericRegisterReference reg)
        {
            SourceRegister = reg;
        }

        public override string ToString()
        {
            return $"buzzer := {SourceRegister}";
        }
    }

    public class MemoryAssignment : Assignment
    {
        public NumericLiteral? Address { get; }
        public NamedReference? Name { get; }
        public RValue ResolvedValue { get; private set; }

        public MemoryAssignment(Token token, NumericLiteral address) : base(token) { Address = address; }
        public MemoryAssignment(Token token, NamedReference name) : base(token) { Name = name; }

        public void Resolve(RValue value)
        {
            ResolvedValue = value;
        }

        public override string ToString()
        {
            return $"i := {Address?.ToString() ?? Name?.ToString() ?? "???"}";
        }
    }

    public class MemoryIncrementAssignment(Token token, GenericRegisterReference reg) : Assignment(token)
    {
        public GenericRegisterReference Register { get; private set; } = reg;

        public void Resolve(GenericRegisterReference reg)
        {
            Register = reg;
        }

        public override string ToString()
        {
            return $"i += {Register}";
        }
    }

    public class LoadCharacterAssignment(Token token, GenericRegisterReference reg) : Assignment(token)
    {
        public GenericRegisterReference Register { get; private set; } = reg;

        public void Resolve(GenericRegisterReference reg)
        {
            Register = reg;
        }

        public override string ToString()
        {
            return $"i := hex {Register}";
        }
    }

    public class KeyInputAssignment(Token token, GenericRegisterReference reg) : Assignment(token)
    {
        public GenericRegisterReference DestinationRegister { get; private set; } = reg;

        public void Resolve(GenericRegisterReference reg)
        {
            DestinationRegister = reg;
        }

        public override string ToString()
        {
            return $"{DestinationRegister} := key";
        }
    }

    #region register-register assignment
    public abstract class RegisterAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src,
        RegisterAssignment.AssignmentOperator op) : Assignment(token)
    {
        public GenericRegisterReference SourceRegister { get; private set; } = dest;
        public GenericRegisterReference DestinationRegister { get; private set; } = src;
        public AssignmentOperator Operator { get; } = op;

        public void ResolveDestination(GenericRegisterReference reg)
        {
            DestinationRegister = reg;
        }

        public void ResolveSource(GenericRegisterReference reg)
        {
            SourceRegister = reg;
        }

        public override string ToString()
        {
            return $"{DestinationRegister} {operatorText[Operator]} {SourceRegister}";
        }

        private static readonly Dictionary<AssignmentOperator, string> operatorText = new (){
            {AssignmentOperator.Assignment, ":="},
            {AssignmentOperator.Addition, "+="},
            {AssignmentOperator.Subtraction, "-="},
            {AssignmentOperator.ReverseSubtraction, "=-"},
            {AssignmentOperator.Or, "|="},
            {AssignmentOperator.And, "&="},
            {AssignmentOperator.Xor, "^="},
            {AssignmentOperator.LeftShift, "<<="},
            {AssignmentOperator.RightShift, ">>="},
        };

        public enum AssignmentOperator
        {
            Assignment,
            Addition,
            Subtraction,
            ReverseSubtraction,
            Or,
            And,
            Xor,
            LeftShift,
            RightShift
        }
    }

    public class RegisterCopyAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.Assignment)
    {

    }

    public class RegisterAddAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.Addition)
    {

    }

    public class RegisterSubtractionAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.Subtraction)
    {

    }

    public class RegisterReverseSubtractionAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.ReverseSubtraction)
    {

    }

    public class RegisterAndAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.And)
    {

    }

    public class RegisterOrAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.Or)
    {

    }

    public class RegisterXorAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.Xor)
    {

    }

    public class RegisterLeftShiftAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.LeftShift)
    {

    }

    public class RegisterRightShiftAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.RightShift)
    {

    }
    #endregion

    public class ImmediateAssignment(
        Token token,
        GenericRegisterReference reg,
        RValue value,
        ImmediateAssignment.Operator op = ImmediateAssignment.Operator.Assignment) : Assignment(token)
    {
        public GenericRegisterReference DestinationRegister { get; private set; } = reg;
        public RValue Value { get; private set; } = value;
        public Operator Op { get; protected set; } = op;

        public void ResolveDestination(GenericRegisterReference reg)
        {
            DestinationRegister = reg;
        }

        public void ResolveArgument(RValue value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return $"{DestinationRegister} {operatorText[Op]} {Value}";
        }

        public enum Operator
        {
            Assignment,
            Addition,
            Subtraction,
        }

        internal static readonly Dictionary<Operator, string> operatorText = new()
        {
            {Operator.Assignment, ":="},
            {Operator.Addition, "+="},
            {Operator.Subtraction, "-="},
        };
    }

    public class ImmediateAdditionAssignment(
        Token token,
        GenericRegisterReference reg,
        RValue value) :
        ImmediateAssignment(token, reg, value, Operator.Addition)
    {
    }

    public class ImmediateSubtractionAssignment(
        Token token,
        GenericRegisterReference reg,
        RValue value) :
        ImmediateAssignment(token, reg, value, Operator.Subtraction)
    {
    }

    public class AmbiguousAssignment(
       Token token,
       GenericRegisterReference reg,
       NamedReference name,
       ImmediateAssignment.Operator op = ImmediateAssignment.Operator.Assignment) : Assignment(token)
    {
        public GenericRegisterReference DestinationRegister { get; } = reg;
        public NamedReference Name { get; } = name;
        public ImmediateAssignment.Operator Op { get; protected set; } = op;

        public override string ToString()
        {
            return $"{DestinationRegister} {ImmediateAssignment.operatorText[Op]} {Name}";
        }
    }

    public class AmbiguousAdditionAssignment(
        Token token,
        GenericRegisterReference reg,
        NamedReference name) :
        AmbiguousAssignment(token, reg, name, ImmediateAssignment.Operator.Addition)
    {
    }

    public class AmbiguousSubtractionAssignment(
        Token token,
        GenericRegisterReference reg,
        NamedReference name) :
        AmbiguousAssignment(token, reg, name, ImmediateAssignment.Operator.Subtraction)
    {
    }

    public class RandomAssignment(
        Token token,
        GenericRegisterReference reg,
        RValue mask) : Assignment(token)
    {
        public GenericRegisterReference DestinationRegister { get; private set; } = reg;
        public RValue Mask { get; private set; } = mask;

        public void ResolveRegister(GenericRegisterReference reg)
        {
            DestinationRegister = reg;
        }

        public void ResolveMask(RValue value)
        {
            Mask = value;
        }

        public override string ToString()
        {
            return $"{DestinationRegister} := rand {Mask}";
        }
    }

    #endregion

    #region directives
    public abstract class Directive(Token token) : Statement(token)
    {

    }

    public class BreakpointDirective(Token token, string name) : Directive(token)
    {
        public string Name { get; } = name;

        public override string ToString()
        {
            return $":breakpoint {Name}";
        }
    }

    public class MonitorDirective(Token token, string text) : Directive(token)
    {
        private readonly string text = text;

        public override string ToString()
        {
            return text;
        }
    }

    public abstract class Alias(Token token, string name) : Directive(token)
    {
        public string Name { get; } = name;
    }

    public class RegisterAlias(Token token, string name, byte index) : Alias(token, name)
    {
        public byte RegisterIndex { get; } = index;

        public override string ToString()
        {
            return $":alias {Name} v{RegisterIndex:X}";
        }
    }

    public class ConstantAlias(Token token, string name, CalculationExpression expr) : Alias(token, name)
    {
        public CalculationExpression Expression { get; } = expr;

        public override string ToString()
        {
            return $":alias {Name} {Expression}";
        }
    }

    public class LabelDeclaration(Token token, string name) : Directive(token)
    {
        public string Name { get; } = name;

        public override string ToString()
        {
            return $": {Name}";
        }
    }

    public abstract class FunctionCall(Token token) : Directive(token)
    {
    }

    public class FunctionCallByName(Token token, NamedReference name) : FunctionCall(token)
    {
        public NamedReference Name { get; } = name;

        public override string ToString()
        {
            return $":call {Name}";
        }
    }

    public class FunctionCallByNumber(Token token, CalculationExpression expr) : FunctionCall(token)
    {
        public CalculationExpression Expression { get; } = expr;

        public override string ToString()
        {
            return $":call {Expression}";
        }
    }

    public class ConstantDirective(Token token, string name, RValue value) : Directive(token)
    {
        public string Name { get; } = name;
        public RValue Value { get; } = value;

        public override string ToString()
        {
            return $":const {Name} {Value}";
        }
    }

    public abstract class UnpackDirective(Token token, NamedReference name) : Directive(token)
    {
        public NamedReference Name { get; } = name;
        public RValue ResolvedName { get; private set; }

        public void ResolveName(RValue value)
        {
            ResolvedName = value;
        }
    }

    public class UnpackNumberDirective(Token token, NamedReference name, RValue value) : UnpackDirective(token, name)
    {
        public RValue Value { get; private set; } = value;

        public void Resolve(RValue value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return $":unpack {Value} {Name}";
        }
    }

    public class UnpackLongDirective(Token token, NamedReference name) : UnpackDirective(token, name)
    {
        public override string ToString()
        {
            return $":unpack long {Name}";
        }
    }

    public class NextDirective(Token token, string label, Statement s) : Directive(token)
    {
        public string Label { get; } = label;
        public Statement Statement { get; } = s;

        public override string ToString()
        {
            return $":next {Label} {Statement}";
        }
    }

    public class OrgDirective(Token token, CalculationExpression expr) : Directive(token)
    {
        public CalculationExpression Expression { get; } = expr;

        public override string ToString()
        {
            return $":org {Expression}";
        }
    }

    public class MacroDefinition(Token token, string name, string[] arguments, Statement[] body) : Directive(token)
    {
        public string Name { get; } = name;
        public string[] Arguments { get; } = arguments;
        public Statement[] Body { get; } = body;

        public override string ToString()
        {
            return $"{Name} {string.Join(" ", Arguments)} {{...}}";
        }
    }

    public class Calculation(Token token, string name, CalculationExpression expr) : Directive(token)
    {
        public string Name { get; } = name;
        public CalculationExpression Expression { get; } = expr;

        public override string ToString()
        {
            return $":calc {Name} {Expression}";
        }
    }

    public abstract class ByteDirective(Token token) : Directive(token)
    {

    }

    public class ValueByteDirective(Token token, RValue value) : ByteDirective(token)
    {
        public RValue Value { get; private set; } = value;

        public void Resolve(RValue value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return $":byte {Value}";
        }
    }

    public class ExpressionByteDirective(Token token, CalculationExpression expr) : ByteDirective(token)
    {
        public CalculationExpression Expression { get; } = expr;

        public override string ToString()
        {
            return $":byte {Expression}";
        }
    }

    public abstract class PointerDirective(Token token) : Directive(token)
    {
    }

    public class NamedPointerDirective(Token token, NamedReference name) : PointerDirective(token)
    {
        public NamedReference Name { get; } = name;
        public RValue ResolvedValue { get; private set; }

        public void Resolve(RValue value)
        {
            ResolvedValue = value;
        }

        public override string ToString()
        {
            return $":pointer {Name}";
        }
    }

    public class PointerExpressionDirective(Token token, CalculationExpression expr) : PointerDirective(token)
    {
        public CalculationExpression Expression { get; } = expr;

        public override string ToString()
        {
            return $":pointer {Expression}";
        }
    }

    public class StringDirective(Token token, string name, string alphabet, Statement[] body) : Directive(token)
    {
        public string Name { get; } = name;
        public string Alphabet { get; } = alphabet;
        public Statement[] Body { get; } = body;

        public override string ToString()
        {
            return $":stringmode {Name} \"{Alphabet}\" {{...}}";
        }
    }

    public class AssertDirective(Token token, CalculationExpression expr, string? msg = null) : Directive(token)
    {
        public CalculationExpression Expression { get; } = expr;
        public string? Message { get; } = msg;

        public override string ToString()
        {
            return $":assert {Message} {Expression}";
        }
    }
    #endregion

    #region conditional expression
    public abstract class ConditionalExpression(Token token, RValue reg) : AstNode(token)
    {
        public RValue LeftRegister { get; private set; } = reg;

        public void ResolveLeft(RValue value)
        {
            LeftRegister = value;
        }
    }

    public class BinaryConditionalExpression(
        Token token,
        RValue left,
        BinaryConditionalExpression.ConditionalOperator op,
        RValue right)
        : ConditionalExpression(token, left)
    {
        public ConditionalOperator Operator { get; protected set; } = op;
        public RValue RightHandOperand { get; private set; } = right;

        public void ResolveRight(RValue value)
        {
            RightHandOperand = value;
        }

        public override string ToString()
        {
            return $"{LeftRegister} {operatorText[Operator]} {RightHandOperand}";
        }

        public enum ConditionalOperator
        {
            Equality,
            Inequality,
            LessThan,
            LessThanOrEqualTo,
            GreaterThan,
            GreaterThanOrEqualTo,
        }

        private static readonly Dictionary<ConditionalOperator, string> operatorText = new()
        {
            {ConditionalOperator.Equality, "=="},
            {ConditionalOperator.Inequality, "!="},
            {ConditionalOperator.LessThan, "<"},
            {ConditionalOperator.LessThanOrEqualTo, "<="},
            {ConditionalOperator.GreaterThan, ">"},
            {ConditionalOperator.GreaterThanOrEqualTo, ">="},
        };
    }

    public class KeystrokeCondition(Token token, RValue reg, bool pressed) : ConditionalExpression(token, reg)
    {
        public bool KeyPressed { get; } = pressed;

        public override string ToString()
        {
            return $"{LeftRegister} {(KeyPressed ? "" : "-")}key";
        }
    }
    #endregion

    #region control flow statements
    public abstract class ControlFlowStatement(Token token) : Statement(token)
    {

    }
    
    public class IfStatement(Token token, ConditionalExpression expr, Statement? s = null) : ControlFlowStatement(token)
    {
        public ConditionalExpression Condition { get; } = expr;
        public Statement? Body { get; } = s;

        public override string ToString()
        {
            return $"if {Condition} then {Body?.ToString()}";
        }
    }

    public class IfElseBlock(
        Token token,
        ConditionalExpression expr,
        Statement[] thenBlock,
        Statement[] elseBlock) :
        ControlFlowStatement(token)
    {
        public ConditionalExpression Condition { get; } = expr;
        public Statement[] ThenBody { get; } = thenBlock;
        public Statement[] ElseBody { get; } = elseBlock;

        public override string ToString()
        {
            return $"if {Condition} begin ...{(ElseBody.Length > 0 ? " else ..." : "")} end";
        }
    }

    public class LoopStatement(Token token, Statement[] body) : ControlFlowStatement(token)
    {
        public Statement[] Body { get; } = body;

        public override string ToString()
        {
            return $"loop {(Body.Length > 0 ? "... " : "")}again";
        }
    }

    public class WhileStatement(Token token, ConditionalExpression expr, Statement s) : ControlFlowStatement(token)
    {
        public ConditionalExpression Condition { get; } = expr;
        public Statement Statement { get; } = s;

        public override string ToString()
        {
            return $"while {Condition} {Statement}";
        }
    }
    #endregion

    #region calculations
    public abstract class CalculationExpression(Token token) : AstNode(token)
    {
        public abstract double Interpret();
    }

    public class CalculationBinaryOperation(
        Token token,
        CalculationExpression lhs,
        CalculationExpression rhs,
        CalculationBinaryOperation.BinaryOperator @operator)
        : CalculationExpression(token)
    {
        public CalculationExpression Left { get; } = lhs;
        public CalculationExpression Right { get; } = rhs;
        public BinaryOperator Operator { get; } = @operator;

        public override double Interpret()
        {
            double l = Left.Interpret();
            double r = Right.Interpret();
            return Operator switch
            {
                BinaryOperator.Addition => l + r,
                BinaryOperator.Subtraction => l - r,
                BinaryOperator.Multiplication => l * r,
                BinaryOperator.Division => l / r,
                BinaryOperator.Modulus => l % r,
                BinaryOperator.Minimum => Math.Min(l, r),
                BinaryOperator.Maximum => Math.Max(l, r),
                BinaryOperator.Exponentiation => Math.Pow(l, r),

                BinaryOperator.And => UInt32Wrapper(l, r, (l, r) => l & r),
                BinaryOperator.Or => UInt32Wrapper(l, r, (l, r) => l | r),
                BinaryOperator.Xor => UInt32Wrapper(l, r, (l, r) => l ^ r),
                BinaryOperator.LeftShift => UInt32Wrapper(l, r, (l, r) => l << (int)r),
                BinaryOperator.RightShift => UInt32Wrapper(l, r, (l, r) => l >> (int)r),

                BinaryOperator.LessThan => ToBinary(l < r),
                BinaryOperator.LessThanOrEqual => ToBinary(l <= r),
                BinaryOperator.GreaterThan => ToBinary(l > r),
                BinaryOperator.GreaterThanOrEqual => ToBinary(l >= r),
                BinaryOperator.EqualTo => ToBinary(l == r),
                BinaryOperator.NotEqualTo => ToBinary(l != r),

                _ => throw new Exception("unknown binary operator")
            };
        }

        public enum BinaryOperator
        {
            Addition,
            Subtraction,
            Multiplication,
            Division,
            Modulus,
            Minimum,
            Maximum,
            Exponentiation,

            And,
            Or,
            Xor,
            LeftShift,
            RightShift,

            LessThan,
            LessThanOrEqual,
            GreaterThan,
            GreaterThanOrEqual,
            EqualTo,
            NotEqualTo
        }

        public override string ToString()
        {
            if (Operator == BinaryOperator.Minimum ||
                Operator == BinaryOperator.Maximum)
            {
                return $"{Operator.ToString().ToLower()[..3]}({Left}, {Right})";
            }
            return $"({Left} {operatorText[Operator]} {Right})";
        }

        private static UInt32 UInt32Wrapper(double l, double r, Func<UInt32, UInt32, UInt32> function)
        {
            return function.Invoke(Convert.ToUInt32(l), Convert.ToUInt32(r));
        }

        private static int ToBinary(bool b)
        {
            return b ? 1 : 0;
        }

        private static readonly Dictionary<BinaryOperator, string> operatorText = new()
        {
            {BinaryOperator.Addition, "+" },
            {BinaryOperator.Subtraction, "-"},
            {BinaryOperator.Multiplication, "*"},
            {BinaryOperator.Division, "/"},
            {BinaryOperator.Modulus, "%"},
            {BinaryOperator.Exponentiation, "**"},
            {BinaryOperator.And, "&" },
            {BinaryOperator.Or, "|" },
            {BinaryOperator.Xor, "^"},
            {BinaryOperator.LeftShift, "<<" },
            {BinaryOperator.RightShift, ">>" },
            {BinaryOperator.LessThan, "<"},
            {BinaryOperator.LessThanOrEqual, "<=" },
            {BinaryOperator.GreaterThan, ">"},
            {BinaryOperator.GreaterThanOrEqual, ">=" },
            {BinaryOperator.EqualTo, "=="},
            {BinaryOperator.NotEqualTo, "!="},
        };
    }

    public class CalculationUnaryOperation(
        Token token,
        CalculationExpression expr,
        CalculationUnaryOperation.UnaryOperator @operator)
        : CalculationExpression(token)
    {
        public CalculationExpression Expression { get; } = expr;
        public UnaryOperator Operator { get; } = @operator;

        public override double Interpret()
        {
            double e = Expression.Interpret();
            return Operator switch
            {
                UnaryOperator.NumericalNegation => -e,
                UnaryOperator.BitwiseNegation => ~Convert.ToUInt32(e),
                UnaryOperator.LogicalNegation => Convert.ToDouble(!Convert.ToBoolean(e)),
                UnaryOperator.AddressOf => throw new NotImplementedException("TODO"),
                UnaryOperator.Sine => Math.Sin(e),
                UnaryOperator.Cosine => Math.Cos(e),
                UnaryOperator.Tangent => Math.Tan(e),
                UnaryOperator.Logarithm => Math.Log(e),
                UnaryOperator.Exponentiation => Math.Exp(e),
                UnaryOperator.AbsoluteValue => Math.Abs(e),
                UnaryOperator.SquareRoot => Math.Sqrt(e),
                UnaryOperator.Sign => Math.Sign(e),
                UnaryOperator.Ceiling => Math.Ceiling(e),
                UnaryOperator.Floor => Math.Floor(e),

                _ => throw new NotImplementedException("unknown binary operator")
            };
        }

        public override string ToString()
        {
            return $"({operatorText[Operator]}{Expression})";
        }

        public enum UnaryOperator
        {
            NumericalNegation,
            BitwiseNegation,
            LogicalNegation,
            AddressOf,
            Sine,
            Cosine,
            Tangent,
            Exponentiation,
            Logarithm,
            AbsoluteValue,
            SquareRoot,
            Sign,
            Ceiling,
            Floor,
        }

        private static readonly Dictionary<UnaryOperator, string> operatorText = new()
        {
            {UnaryOperator.NumericalNegation, "-" },
            {UnaryOperator.BitwiseNegation, "~" },
            {UnaryOperator.LogicalNegation, "!" },
            {UnaryOperator.AddressOf, "@" },
            {UnaryOperator.Sine, "sin " },
            {UnaryOperator.Cosine, "cos " },
            {UnaryOperator.Tangent, "tan " },
            {UnaryOperator.Exponentiation, "e ** " },
            {UnaryOperator.Logarithm, "log " },
            {UnaryOperator.AbsoluteValue, "abs " },
            {UnaryOperator.SquareRoot, "sqrt " },
            {UnaryOperator.Sign, "sign " },
            {UnaryOperator.Ceiling, "ceil " },
            {UnaryOperator.Floor, "floor " },
        };
    }

    public class CalculationStringLengthOperation(Token token, string s, bool text) : CalculationExpression(token)
    {
        public string? Text { get; } = text ? s : null;
        public string? Name { get; } = text ? null : s;

        public override double Interpret()
        {
            if (Text != null)
            {
                return Text.Length;
            }
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            if (Text != null)
            {
                return $"(strlen \"{Text}\")";
            }
            return $"(strlen {Name})";
        }
    }

    public class CalculationName(Token token, string name) : CalculationExpression(token)
    {
        public string Name { get; } = name;

        public override double Interpret()
        {
            return Name switch
            {
                "E" => Math.E,
                "PI" => Math.PI,
                "CALLS" => throw new NotImplementedException(),
                "HERE" => throw new NotImplementedException(),
                _ => throw new NotImplementedException(),
            };
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class CalculationNumber(Token token, double num) : CalculationExpression(token)
    {
        public double Number { get; } = num;

        public override double Interpret()
        {
            return Number;
        }

        public override string ToString()
        {
            return Number.ToString();
        }
    }
    #endregion

    public abstract class RValue(Token token) : AstNode(token)
    {
    }

    public class GenericRegisterReference : RValue
    {
        private bool resolved = false;
        public CalculationExpression? Expression { get; private set; }
        public NamedReference? Name { get; private set; }
        public byte? Index { get; private set; }

        public GenericRegisterReference(Token token, byte index) : base(token) { Index = index; resolved = true; }
        public GenericRegisterReference(Token token, NamedReference name) : base(token) { Name = name; }

        public void Resolve(byte index)
        {
            Index = index;
            Name = null;
            resolved = true;
        }

        public void Resolve(CalculationExpression expression)
        {
            Expression = expression;
            resolved = true;
        }

        public override string ToString()
        {
            if (Index.HasValue)
            {
                return $"v{Index.Value:X}";
            }
            else if (Expression != null)
            {
                return $"v[{Expression}]";
            }
            return Name.ToString();
        }
    }

    public class NamedReference(Token token, string name) : RValue(token)
    {
        public string Name { get; } = name;

        public NumericLiteral? Value { get; private set; }
        public CalculationExpression? Expression { get; private set; }
        public bool Constant { get; private set; } = false;
        public bool Label { get; private set; } = false;
        public CalculationExpression? RegisterIndexExpression { get; private set; }
        public byte? RegisterIndex { get; private set; }

        public void ResolveToNumber(NumericLiteral value) { Value = value; }
        public void ResolveToLabel() { Label = true; }
        public void ResolveToExpression(CalculationExpression expression) { Expression = expression; }
        public void ResolveToKeyword() { Constant = true; }
        public void ResolveToRegister(CalculationExpression expression) { RegisterIndexExpression = expression; }
        public void ResolveToRegister(byte index) { RegisterIndex = index; }

        public override string ToString()
        {
            if (Constant) return Name;
            if (Label) return $"@{Name}";

            if (Value != null) return Value.ToString();
            if (Expression != null) return $"[{Expression}]";
            if (RegisterIndex != null) return $"v{RegisterIndex:X}";
            if (RegisterIndexExpression != null) return $"v[{RegisterIndexExpression}]";

            return Name;
        }
    }

    public class NumericLiteral(Token token, double value) : RValue(token)
    {
        public double Value { get; } = value;

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
