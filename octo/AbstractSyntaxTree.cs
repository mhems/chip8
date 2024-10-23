using static octo.Lexer;
using System.Text.RegularExpressions;
using System.Text;
using System.Numerics;
using System;
using System.Reflection;
using static octo.ImmediateAssignment;
using System.Net;
using System.Xml.Linq;
using Microsoft.Win32;
using System.Drawing;

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
        public GenericRegisterReference Register { get; } = reg;

        public override string ToString()
        {
            return $"bcd {Register}";
        }
    }

    public class SaveStatement(Token token, GenericRegisterReference reg) : Statement(token)
    {
        public GenericRegisterReference Register { get; } = reg;

        public override string ToString()
        {
            return $"save {Register}";
        }
    }

    public class LoadStatement(Token token, GenericRegisterReference reg) : Statement(token)
    {
        public GenericRegisterReference Register { get; } = reg;

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
        public GenericRegisterReference XRegister { get; } = x;
        public GenericRegisterReference YRegister { get; } = y;
        public RValue Height { get; } = height;

        public override string ToString()
        {
            return $"sprite {XRegister} {YRegister} {Height}";
        }
    }

    public abstract class JumpStatement(Token token, bool immediate) : Statement(token)
    {
        protected bool immediate = immediate;
        protected NamedReference? targetName;
        protected NumericLiteral? targetBaseAddress;

        public JumpStatement(Token token, NamedReference target, bool immediate) : this(token, immediate) { targetName = target; }
        public JumpStatement(Token token, NumericLiteral address, bool immediate) : this(token, immediate) { targetBaseAddress = address; }
    }

    public class JumpImmediateStatement : JumpStatement
    {
        public JumpImmediateStatement(Token token, NamedReference target) : base(token, target, true) { }
        public JumpImmediateStatement(Token token, NumericLiteral address) : base(token, address, true) { }

        public override string ToString()
        {
            return $"jump {targetName?.ToString() ?? targetBaseAddress?.ToString() ?? "???"}";
        }
    }

    public class JumpAdditiveStatement : JumpStatement
    {
        public JumpAdditiveStatement(Token token, NamedReference target) : base(token, target, false) { }
        public JumpAdditiveStatement(Token token, NumericLiteral address) : base(token, address, false) { }

        public override string ToString()
        {
            return $"jump0 {targetName?.ToString() ?? targetBaseAddress?.ToString() ?? "???"}";
        }
    }

    public class MacroInvocation(Token token, string name, string[] args) : Statement(token)
    {
        public string MacroName { get; } = name;
        public string[] Arguments { get; } = args;

        public override string ToString()
        {
            return $"name {string.Join(", ", args)}";
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
        public GenericRegisterReference SourceRegister { get; } = reg;

        public override string ToString()
        {
            return $"delay := {SourceRegister}";
        }
    }

    public class SaveDelayAssignment(Token token, GenericRegisterReference reg) : Assignment(token)
    {
        public GenericRegisterReference DestinationRegister { get; } = reg;

        public override string ToString()
        {
            return $"{DestinationRegister} := delay";
        }
    }

    public class LoadBuzzerAssignment(Token token, GenericRegisterReference reg) : Assignment(token)
    {
        public GenericRegisterReference SourceRegister { get; } = reg;

        public override string ToString()
        {
            return $"buzzer := {SourceRegister}";
        }
    }

    public class MemoryAssignment : Assignment
    {
        protected NumericLiteral? address;
        protected NamedReference? name;

        public MemoryAssignment(Token token, NumericLiteral address) : base(token) { this.address = address; }
        public MemoryAssignment(Token token, NamedReference name) : base(token) { this.name = name; }

        public override string ToString()
        {
            return $"i := {address?.ToString() ?? name?.ToString() ?? "???"}";
        }
    }

    public class MemoryIncrementAssignment(Token token, GenericRegisterReference reg) : Assignment(token)
    {
        public GenericRegisterReference Register { get; } = reg;

        public override string ToString()
        {
            return $"i += {Register}";
        }
    }

    public class LoadCharacterAssignment(Token token, GenericRegisterReference reg) : Assignment(token)
    {
        public GenericRegisterReference Register { get; } = reg;

        public override string ToString()
        {
            return $"i := hex {Register}";
        }
    }

    public class KeyInputAssignment(Token token, GenericRegisterReference reg) : Assignment(token)
    {
        public GenericRegisterReference DestinationRegister { get; } = reg;

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
        public GenericRegisterReference SourceRegister { get; } = dest;
        public GenericRegisterReference DestinationRegister { get; } = src;
        public AssignmentOperator Operator { get; } = op;

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
        RegisterAssignment(token, dest, src, RegisterAssignment.AssignmentOperator.Assignment)
    {

    }

    public class RegisterAddAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, RegisterAssignment.AssignmentOperator.Addition)
    {

    }

    public class RegisterSubtractionAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, RegisterAssignment.AssignmentOperator.Subtraction)
    {

    }

    public class RegisterReverseSubtractionAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, RegisterAssignment.AssignmentOperator.ReverseSubtraction)
    {

    }

    public class RegisterAndAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, RegisterAssignment.AssignmentOperator.And)
    {

    }

    public class RegisterOrAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, RegisterAssignment.AssignmentOperator.Or)
    {

    }

    public class RegisterXorAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, RegisterAssignment.AssignmentOperator.Xor)
    {

    }

    public class RegisterLeftShiftAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, RegisterAssignment.AssignmentOperator.LeftShift)
    {

    }

    public class RegisterRightShiftAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, RegisterAssignment.AssignmentOperator.RightShift)
    {

    }
    #endregion

    public class ImmediateAssignment(
        Token token,
        GenericRegisterReference reg,
        RValue value,
        Operator op = Operator.Assignment) : Assignment(token)
    {
        public GenericRegisterReference DestinationRegister { get; } = reg;
        public RValue Value { get; } = value;
        public Operator Op { get; protected set; } = op;

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

        private static readonly Dictionary<Operator, string> operatorText = new()
        {
            {Operator.Assignment, ":="},
            {Operator.Addition, "+="},
            {Operator.Subtraction, "-="},
        };
    }

    public class ImmediateAdditionAssignment(
        Token token,
        GenericRegisterReference reg, RValue value) :
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

    public class RandomAssignment(
        Token token,
        GenericRegisterReference reg,
        RValue mask) : Assignment(token)
    {
        public GenericRegisterReference DestinationRegister { get; } = reg;
        public RValue Mask { get; } = mask;

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

    public class DebuggingDirective(Token token, string text) : Directive(token)
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

    public class FunctionCall(Token token, CalculationExpression expr) : Directive(token)
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
    }

    public class UnpackNumberDirective(Token token, NamedReference name, RValue value) : UnpackDirective(token, name)
    {
        public RValue Value { get; } = value;

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

    public class MacroDefinition(Token token, string name, RValue[] arguments, Statement[] body) : Directive(token)
    {
        public string Name { get; } = name;
        public RValue[] Arguments { get; } = arguments;
        public Statement[] Body { get; } = body;

        public override string ToString()
        {
            return $"{Name} {string.Join(" ", Arguments.Select(a => a.ToString()))} {{...}}";
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
        public RValue Value { get; } = value;

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

    public class PointerDirective(Token token, CalculationExpression expr) : Directive(token)
    {
        public CalculationExpression Expression { get; } = expr;

        public override string ToString()
        {
            return $":pointer {Expression}";
        }
    }

    public class StringDirective(Token token, string name, string text, Statement[] body) : Directive(token)
    {
        public string Name { get; } = name;
        public string Text { get; } = text;
        public Statement[] Body { get; } = body;

        public override string ToString()
        {
            return $":stringmode {Name} \"{Text}\" {{...}}";
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
        public RValue LeftRegister { get; } = reg;
    }

    public class BinaryConditionalExpression(
        Token token,
        RValue left,
        BinaryConditionalExpression.ConditionalOperator op,
        RValue right)
        : ConditionalExpression(token, left)
    {
        public ConditionalOperator Operator { get; protected set; } = op;
        public RValue RightHandOperand { get; } = right;

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
    public class IfStatement(Token token, ConditionalExpression expr, Statement? s = null) : Statement(token)
    {
        public ConditionalExpression Condition { get; } = expr;
        public Statement? Body { get; } = s;

        public override string ToString()
        {
            return $"if {Condition} then {Body?.ToString()}";
        }
    }

    public class IfElseBlock(Token token, ConditionalExpression expr, Statement[] thenBlock, Statement[] elseBlock) : Statement(token)
    {
        public ConditionalExpression Condition { get; } = expr;
        public Statement[] ThenBody { get; } = thenBlock;
        public Statement[] ElseBody { get; } = elseBlock;

        public override string ToString()
        {
            return $"if {Condition} begin ...{(ElseBody.Length > 0 ? " else ..." : "")} end";
        }
    }

    public class LoopStatement(Token token, Statement[] body) : Statement(token)
    {
        public Statement[] Body { get; } = body;

        public override string ToString()
        {
            return $"loop {(Body.Length > 0 ? "... " : "")}again";
        }
    }

    public class WhileStatement(Token token, ConditionalExpression expr, Statement s) : Statement(token)
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
        public readonly CalculationExpression lhs = lhs;
        public readonly CalculationExpression rhs = rhs;
        public readonly BinaryOperator @operator = @operator;

        public override double Interpret()
        {
            double l = lhs.Interpret();
            double r = rhs.Interpret();
            return @operator switch
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
            if (@operator == BinaryOperator.Minimum ||
                @operator == BinaryOperator.Maximum)
            {
                return $"{@operator.ToString().ToLower()[..3]}({lhs}, {rhs})";
            }
            return $"({lhs} {operatorText[@operator]} {rhs})";
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
        public readonly CalculationExpression expr = expr;
        public readonly UnaryOperator @operator = @operator;

        public override double Interpret()
        {
            double e = expr.Interpret();
            return @operator switch
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
            return $"({operatorText[@operator]}{expr})";
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
        public string? Name { get; }
        public byte? Index { get; }

        public GenericRegisterReference(Token token, byte index) : base(token) { Index = index; }
        public GenericRegisterReference(Token token, string name) : base(token) { Name = name; }

        public override string ToString()
        {
            if (Index.HasValue)
            {
                return $"v{Index.Value:X}";
            }
            return Name.ToString();
        }
    }

    public class NamedReference(Token token, string name) : RValue(token)
    {
        public string Name { get; } = name;

        public override string ToString()
        {
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
