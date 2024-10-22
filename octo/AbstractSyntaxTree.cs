using static octo.Lexer;
using System.Text.RegularExpressions;
using System.Text;
using System.Numerics;
using System;
using System.Reflection;
using static octo.ImmediateAssignment;

namespace octo
{
    public abstract class AstNode
    {

    }

    #region statements
    public abstract class Statement : AstNode
    {

    }

    public class ReturnStatement : Statement { }
    
    public class ClearStatement : Statement { }

    public class BcdStatement(GenericRegisterReference reg) : Statement
    {
        public GenericRegisterReference Register { get; } = reg;
    }

    public class SaveStatement(GenericRegisterReference reg) : Statement
    {
        public GenericRegisterReference Register { get; } = reg;
    }

    public class LoadStatement(GenericRegisterReference reg) : Statement
    {
        public GenericRegisterReference Register { get; } = reg;
    }

    public class SpriteStatement(GenericRegisterReference x, GenericRegisterReference y, RValue height) : Statement
    {
        public GenericRegisterReference XRegister { get; } = x;
        public GenericRegisterReference YRegister { get; } = y;
        public RValue Height { get; } = height;
    }

    public abstract class JumpStatement(bool immediate) : Statement
    {
        protected bool immediate = immediate;
        protected NamedReference? targetName;
        protected NumericLiteral? targetBaseAddress;

        public JumpStatement(NamedReference target, bool immediate) : this(immediate) { targetName = target; }
        public JumpStatement(NumericLiteral address, bool immediate) : this(immediate) { targetBaseAddress = address; }
    }

    public class JumpImmediateStatement : JumpStatement
    {
        public JumpImmediateStatement(NamedReference target) : base(target, true) { }
        public JumpImmediateStatement(NumericLiteral address) : base(address, true) { }
    }

    public class JumpAdditiveStatement : JumpStatement
    {
        public JumpAdditiveStatement(NamedReference target) : base(target, false) { }
        public JumpAdditiveStatement(NumericLiteral address) : base(address, false) { }
    }

    public class MacroInvocation(string name, string[] args) : Statement
    {
        public string MacroName { get; } = name;
        public string[] Arguments { get; } = args;
    }

    public class AmbiguousCall(string name) : Statement
    {
        public string TargetName { get; } = name;
    }

    public class DataDeclaration(byte value) : Statement
    {
        public byte Value { get; } = value;
    }
    #endregion

    #region assignments
    public abstract class Assignment : Statement
    {

    }

    public class LoadDelayAssignment(GenericRegisterReference reg) : Assignment
    {
        public GenericRegisterReference SourceRegister { get; } = reg;
    }

    public class SaveDelayAssignment(GenericRegisterReference reg) : Assignment
    {
        public GenericRegisterReference DestinationRegister { get; } = reg;
    }

    public class LoadBuzzerAssignment(GenericRegisterReference reg) : Assignment
    {
        public GenericRegisterReference SourceRegister { get; } = reg;
    }

    public class MemoryAssignment : Assignment
    {
        protected NumericLiteral? address;
        protected NamedReference? name;

        public MemoryAssignment(NumericLiteral address) { this.address = address; }
        public MemoryAssignment(NamedReference name) { this.name = name; }
    }

    public class MemoryIncrementAssignment(GenericRegisterReference reg) : Assignment
    {
        public GenericRegisterReference Register { get; } = reg;
    }

    public class LoadCharacterAssignment(GenericRegisterReference reg) : Assignment
    {
        public GenericRegisterReference Register { get; } = reg;
    }

    public class KeyInputAssignment(GenericRegisterReference reg) : Assignment
    {
        public GenericRegisterReference DestinationRegister { get; } = reg;
    }

    #region register-register assignment
    public abstract class RegisterAssignment(GenericRegisterReference dest, GenericRegisterReference src, RegisterAssignment.AssignmentOperator op) : Assignment
    {
        public GenericRegisterReference SourceRegister { get; } = dest;
        public GenericRegisterReference DestinationRegister { get; } = src;
        public AssignmentOperator Operator { get; } = op;

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

    public class RegisterCopyAssignment(GenericRegisterReference dest, GenericRegisterReference src) : RegisterAssignment(dest, src, RegisterAssignment.AssignmentOperator.Assignment)
    {

    }

    public class RegisterAddAssignment(GenericRegisterReference dest, GenericRegisterReference src) : RegisterAssignment(dest, src, RegisterAssignment.AssignmentOperator.Addition)
    {

    }

    public class RegisterSubtractionAssignment(GenericRegisterReference dest, GenericRegisterReference src) : RegisterAssignment(dest, src, RegisterAssignment.AssignmentOperator.Subtraction)
    {

    }

    public class RegisterReverseSubtractionAssignment(GenericRegisterReference dest, GenericRegisterReference src) : RegisterAssignment(dest, src, RegisterAssignment.AssignmentOperator.ReverseSubtraction)
    {

    }

    public class RegisterAndAssignment(GenericRegisterReference dest, GenericRegisterReference src) : RegisterAssignment(dest, src, RegisterAssignment.AssignmentOperator.And)
    {

    }

    public class RegisterOrAssignment(GenericRegisterReference dest, GenericRegisterReference src) : RegisterAssignment(dest, src, RegisterAssignment.AssignmentOperator.Or)
    {

    }

    public class RegisterXorAssignment(GenericRegisterReference dest, GenericRegisterReference src) : RegisterAssignment(dest, src, RegisterAssignment.AssignmentOperator.Xor)
    {

    }

    public class RegisterLeftShiftAssignment(GenericRegisterReference dest, GenericRegisterReference src) : RegisterAssignment(dest, src, RegisterAssignment.AssignmentOperator.LeftShift)
    {

    }

    public class RegisterRightShiftAssignment(GenericRegisterReference dest, GenericRegisterReference src) : RegisterAssignment(dest, src, RegisterAssignment.AssignmentOperator.RightShift)
    {

    }
    #endregion

    public class ImmediateAssignment(
        GenericRegisterReference reg,
        RValue value,
        Operator op = Operator.Assignment) : Assignment
    {
        public GenericRegisterReference DestinationRegister { get; } = reg;
        public RValue Value { get; } = value;
        public Operator Op { get; protected set; } = op;

        public enum Operator
        {
            Assignment,
            Addition,
            Subtraction,
        }
    }

    public class ImmediateAdditionAssignment(GenericRegisterReference reg, RValue value) :
        ImmediateAssignment(reg, value, Operator.Addition)
    {
    }

    public class ImmediateSubtractionAssignment(GenericRegisterReference reg, RValue value) :
        ImmediateAssignment(reg, value, Operator.Subtraction)
    {
    }

    public class RandomAssignment(GenericRegisterReference reg, RValue mask) : Assignment
    {
        public GenericRegisterReference DestinationRegister { get; } = reg;
        public RValue Mask { get; } = mask;
    }

    #endregion

    #region directives
    public abstract class Directive : Statement
    {

    }
    public abstract class Alias(string name) : Directive
    {
        public string Name { get; } = name;
    }

    public class RegisterAlias(string name, byte index) : Alias(name)
    {
        public byte RegisterIndex { get; } = index;
    }

    public class ConstantAlias(string name, CalculationExpression expr) : Alias(name)
    {
        public CalculationExpression Expression { get; } = expr;
    }

    public class LabelDeclaration(string name) : Directive
    {
        public string Name { get; } = name;
    }

    public class FunctionCall(CalculationExpression expr) : Directive
    {
        public CalculationExpression Expression { get; } = expr;
    }

    public class ConstantDirective(string name, RValue value) : Directive
    {
        public string Name { get; } = name;
        public RValue Value { get; } = value;
    }

    public abstract class UnpackDirective(NamedReference name) : Directive
    {
        public NamedReference Name { get; } = name;
    }

    public class UnpackNumberDirective(NamedReference name, RValue value) : UnpackDirective(name)
    {
        public RValue Value { get; } = value;
    }

    public class UnpackLongDirective(NamedReference name) : UnpackDirective(name)
    {

    }

    public class NextDirective(string label, Statement s) : Directive
    {
        public string Label { get; } = label;
        public Statement Statement { get; } = s;
    }

    public class OrgDirective(CalculationExpression expr) : Directive
    {
        public CalculationExpression Expression { get; } = expr;

        public OrgDirective(ushort address) : this(new CalculationNumber(address)) { }
    }

    public class MacroDefinition(string name, RValue[] arguments, Statement[] body) : Directive
    {
        public string Name { get; } = name;
        public RValue[] Arguments { get; } = arguments;
        public Statement[] Body { get; } = body;
    }

    public class Calculation(string name, CalculationExpression expr) : Directive
    {
        public string Name { get; } = name;
        public CalculationExpression Expression { get; } = expr;
    }

    public abstract class ByteDirective : Directive
    {

    }

    public class ValueByteDirective(RValue value) : ByteDirective
    {
        public RValue Value { get; } = value;
    }

    public class ExpressionByteDirective(CalculationExpression expr) : ByteDirective
    {
        public CalculationExpression Expression { get; } = expr;
    }

    public class PointerDirective(CalculationExpression expr) : Directive
    {
        public CalculationExpression Expression { get; } = expr;
    }

    public class StringDirective(string name, string text, Statement[] body) : Directive
    {
        public string Name { get; } = name;
        public string Text { get; } = text;
        public Statement[] Body { get; } = body;
    }

    public class AssertDirective(CalculationExpression expr, string? msg = null) : Directive
    {
        public CalculationExpression Expression { get; } = expr;
        public string? Message { get; } = msg;
    }

    public abstract class IncludeDirective(string filename) : Directive
    {
        public string FileName { get; } = filename;
    }

    public class FileInclude(string filename) : IncludeDirective(filename)
    {
    }

    public class PixelInclude(string filename, byte width, byte height) : IncludeDirective(filename)
    {
        public byte Width { get; } = width;
        public byte Height { get; } = height;
    }
    #endregion

    #region conditional expression
    public abstract class ConditionalExpression(RValue reg) : AstNode
    {
        public RValue LeftRegister { get; } = reg;
    }

    public class BinaryConditionalExpression(RValue left,
        BinaryConditionalExpression.ConditionalOperator op,
        RValue right)
        : ConditionalExpression(left)
    {
        public ConditionalOperator Operator { get; protected set; } = op;
        public RValue RightHandOperand { get; } = right;

        public enum ConditionalOperator
        {
            Equality,
            Inequality,
            LessThan,
            LessThanOrEqualTo,
            GreaterThan,
            GreaterThanOrEqualTo,
        }
    }

    public class KeystrokeCondition(RValue reg, bool pressed) : ConditionalExpression(reg)
    {
        public bool KeyPressed { get; } = pressed;
    }
    #endregion

    #region control flow statements
    public class IfStatement(ConditionalExpression expr, Statement? s = null) : Statement
    {
        public ConditionalExpression Condition { get; } = expr;
        public Statement? Body { get; } = s;
    }

    public class IfElseBlock(ConditionalExpression expr, Statement[] thenBlock, Statement[] elseBlock) : Statement
    {
        public ConditionalExpression Condition { get; } = expr;
        public Statement[] ThenBody { get; } = thenBlock;
        public Statement[] ElseBody { get; } = elseBlock;
    }

    public class LoopStatement(Statement[] body) : Statement
    {
        public Statement[] Body { get; } = body;
    }

    public class WhileStatement(ConditionalExpression expr, Statement s) : Statement
    {
        public ConditionalExpression Condition { get; } = expr;
        public Statement Statement { get; } = s;
    }
    #endregion

    #region calculations
    public abstract class CalculationExpression : AstNode
    {
        public abstract double Interpret();
    }

    public class CalculationBinaryOperation(
        CalculationExpression lhs,
        CalculationExpression rhs,
        CalculationBinaryOperation.BinaryOperator @operator)
        : CalculationExpression
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

        private static UInt32 UInt32Wrapper(double l, double r, Func<UInt32, UInt32, UInt32> function)
        {
            return function.Invoke(Convert.ToUInt32(l), Convert.ToUInt32(r));
        }

        private static int ToBinary(bool b)
        {
            return b ? 1 : 0;
        }
    }

    public class CalculationUnaryOperation(
        CalculationExpression expr,
        CalculationUnaryOperation.UnaryOperator @operator)
        : CalculationExpression
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
    }

    public class CalculationStringLengthOperation(string s, bool text) : CalculationExpression
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
    }

    public class CalculationName(string name) : CalculationExpression
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
    }

    public class CalculationNumber(double num) : CalculationExpression
    {
        public double Number { get; } = num;

        public override double Interpret()
        {
            return Number;
        }
    }
    #endregion

    public abstract class RValue : AstNode
    {
    }

    public class GenericRegisterReference : RValue
    {
        public string? Name { get; }
        public byte? Index { get; }

        public GenericRegisterReference(byte index) { Index = index; }
        public GenericRegisterReference(string name) { Name = name; }
    }

    public class NamedReference(string name) : RValue
    {
        public string Name { get; } = name;
    }

    public class NumericLiteral(double value) : RValue
    {
        public double Value { get; } = value;
    }
}
