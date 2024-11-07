using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using static octo.Lexer;

namespace octo
{
    public abstract class AstNode(Token token)
    {
        public Token FirstToken { get; } = token;

        public abstract AstNode DeepCopy();
    }

    #region statements
    public abstract class Statement(Token token) : AstNode(token)
    {

    }

    public class ReturnStatement(Token token) : Statement(token)
    {
        public override ReturnStatement DeepCopy()
        {
            return new ReturnStatement(token);
        }

        public override string ToString()
        {
            return "return";
        }
    }

    public class ClearStatement(Token token) : Statement(token)
    {
        public override Statement DeepCopy()
        {
            return new ClearStatement(token);
        }

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

        public override BcdStatement DeepCopy()
        {
            return new BcdStatement(token, Register.DeepCopy() as GenericRegisterReference);
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

        public override AstNode DeepCopy()
        {
            return new SaveStatement(token, Register.DeepCopy() as GenericRegisterReference);
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

        public override AstNode DeepCopy()
        {
            return new LoadStatement(token, Register.DeepCopy() as GenericRegisterReference);
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

        public override SpriteStatement DeepCopy()
        {
            return new SpriteStatement(token,
                XRegister.DeepCopy() as GenericRegisterReference,
                YRegister.DeepCopy() as GenericRegisterReference,
                Height.DeepCopy() as RValue);
        }
        public override string ToString()
        {
            return $"sprite {XRegister} {YRegister} {Height}";
        }
    }

    public abstract class JumpStatement(Token token, RValue target, bool immediate) : Statement(token)
    {
        public bool Immediate { get; } = immediate;
        public RValue Target { get; } = target;

        public override string ToString()
        {
            return $"jump{(Immediate ? "" : "0")} {Target}";
        }
    }

    public class JumpImmediateStatement(Token token, RValue target) : JumpStatement(token, target, true)
    {
        public override JumpImmediateStatement DeepCopy()
        {
            return new JumpImmediateStatement(token, Target);
        }
    }

    public class JumpAdditiveStatement(Token token, RValue target) : JumpStatement(token, target, false)
    {
        public override JumpAdditiveStatement DeepCopy()
        {
            return new JumpAdditiveStatement(token, Target);
        }
    }

    public class MacroInvocation(Token token, string name, string[] args) : Statement(token)
    {
        public string MacroName { get; } = name;
        public string[] Arguments { get; } = args;

        public override MacroInvocation DeepCopy()
        {
            string[] clone = new string[Arguments.Length];
            Array.Copy(Arguments, clone, Arguments.Length);
            return new MacroInvocation(token, MacroName, clone);
        }

        public override string ToString()
        {
            return $"{MacroName} {string.Join(' ', args)}";
        }
    }

    public class AmbiguousCall(Token token, string name) : Statement(token)
    {
        public string TargetName { get; } = name;

        public override AmbiguousCall DeepCopy()
        {
            return new AmbiguousCall(token, TargetName);
        }

        public override string ToString()
        {
            return TargetName;
        }
    }

    public class DataDeclaration(Token token, byte value) : Statement(token)
    {
        public byte Value { get; } = value;

        public override DataDeclaration DeepCopy()
        {
            return new DataDeclaration(token, Value);
        }

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

        public override LoadDelayAssignment DeepCopy()
        {
            return new LoadDelayAssignment(token, SourceRegister.DeepCopy() as GenericRegisterReference);
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

        public override SaveDelayAssignment DeepCopy()
        {
            return new SaveDelayAssignment(token, DestinationRegister.DeepCopy() as GenericRegisterReference);
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

        public override LoadBuzzerAssignment DeepCopy()
        {
            return new LoadBuzzerAssignment(token, SourceRegister.DeepCopy() as GenericRegisterReference);
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
        public RValue? ResolvedValue { get; private set; }

        public MemoryAssignment(Token token, NumericLiteral address) : base(token) { Address = address; }
        public MemoryAssignment(Token token, NamedReference name) : base(token) { Name = name; }

        public void Resolve(RValue value)
        {
            ResolvedValue = value;
            Trace.WriteLine($"MA resolving: {this}: value {value.GetHashCode()}, {ResolvedValue.GetHashCode()}");
        }

        public override MemoryAssignment DeepCopy()
        {
            MemoryAssignment ma;
            if (Name != null)
            {
                ma = new(FirstToken, Name);
            }
            else
            {
                ma = new(FirstToken, Address);
            }
            if (ResolvedValue != null)
            {
                ma.ResolvedValue = ResolvedValue.DeepCopy() as RValue;
            }
            return ma;
        }

        public override string ToString()
        {
            return $"i := {ResolvedValue?.ToString() ?? Address?.ToString() ?? Name?.ToString() ?? "???"}";
        }
    }

    public class MemoryIncrementAssignment(Token token, GenericRegisterReference reg) : Assignment(token)
    {
        public GenericRegisterReference Register { get; private set; } = reg;

        public void Resolve(GenericRegisterReference reg)
        {
            Register = reg;
        }

        public override MemoryIncrementAssignment DeepCopy()
        {
            return new MemoryIncrementAssignment(token, Register.DeepCopy() as GenericRegisterReference);
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

        public override LoadCharacterAssignment DeepCopy()
        {
            return new LoadCharacterAssignment(token, Register.DeepCopy() as GenericRegisterReference);
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

        public override KeyInputAssignment DeepCopy()
        {
            return new KeyInputAssignment(token, DestinationRegister.DeepCopy() as GenericRegisterReference);
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
        public override RegisterCopyAssignment DeepCopy()
        {
            return new RegisterCopyAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                SourceRegister.DeepCopy() as GenericRegisterReference);
        }
    }

    public class RegisterAddAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.Addition)
    {
        public override RegisterAddAssignment DeepCopy()
        {
            return new RegisterAddAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                SourceRegister.DeepCopy() as GenericRegisterReference);
        }
    }

    public class RegisterSubtractionAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.Subtraction)
    {
        public override RegisterSubtractionAssignment DeepCopy()
        {
            return new RegisterSubtractionAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                SourceRegister.DeepCopy() as GenericRegisterReference);
        }
    }

    public class RegisterReverseSubtractionAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.ReverseSubtraction)
    {
        public override RegisterReverseSubtractionAssignment DeepCopy()
        {
            return new RegisterReverseSubtractionAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                SourceRegister.DeepCopy() as GenericRegisterReference);
        }
    }

    public class RegisterAndAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.And)
    {
        public override RegisterAndAssignment DeepCopy()
        {
            return new RegisterAndAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                SourceRegister.DeepCopy() as GenericRegisterReference);
        }
    }

    public class RegisterOrAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.Or)
    {
        public override RegisterOrAssignment DeepCopy()
        {
            return new RegisterOrAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                SourceRegister.DeepCopy() as GenericRegisterReference);
        }
    }

    public class RegisterXorAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.Xor)
    {
        public override RegisterXorAssignment DeepCopy()
        {
            return new RegisterXorAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                SourceRegister.DeepCopy() as GenericRegisterReference);
        }
    }

    public class RegisterLeftShiftAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.LeftShift)
    {
        public override RegisterLeftShiftAssignment DeepCopy()
        {
            return new RegisterLeftShiftAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                SourceRegister.DeepCopy() as GenericRegisterReference);
        }
    }

    public class RegisterRightShiftAssignment(
        Token token,
        GenericRegisterReference dest,
        GenericRegisterReference src) :
        RegisterAssignment(token, dest, src, AssignmentOperator.RightShift)
    {
        public override RegisterRightShiftAssignment DeepCopy()
        {
            return new RegisterRightShiftAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                SourceRegister.DeepCopy() as GenericRegisterReference);
        }
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

        public override ImmediateAssignment DeepCopy()
        {
            return new ImmediateAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                Value.DeepCopy() as RValue,
                Op);
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
        public override ImmediateAdditionAssignment DeepCopy()
        {
            return new ImmediateAdditionAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                Value.DeepCopy() as RValue);
        }
    }

    public class ImmediateSubtractionAssignment(
        Token token,
        GenericRegisterReference reg,
        RValue value) :
        ImmediateAssignment(token, reg, value, Operator.Subtraction)
    {
        public override ImmediateSubtractionAssignment DeepCopy()
        {
            return new ImmediateSubtractionAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                Value.DeepCopy() as RValue);
        }
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

        public override AmbiguousAssignment DeepCopy()
        {
            return new AmbiguousAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                Name.DeepCopy() as NamedReference,
                Op);
        }

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
        public override AmbiguousAdditionAssignment DeepCopy()
        {
            return new AmbiguousAdditionAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                Name.DeepCopy() as NamedReference);
        }
    }

    public class AmbiguousSubtractionAssignment(
        Token token,
        GenericRegisterReference reg,
        NamedReference name) :
        AmbiguousAssignment(token, reg, name, ImmediateAssignment.Operator.Subtraction)
    {
        public override AmbiguousSubtractionAssignment DeepCopy()
        {
            return new AmbiguousSubtractionAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                Name.DeepCopy() as NamedReference);
        }
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

        public override RandomAssignment DeepCopy()
        {
            return new RandomAssignment(token,
                DestinationRegister.DeepCopy() as GenericRegisterReference,
                Mask.DeepCopy() as RValue);
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

        public override BreakpointDirective DeepCopy()
        {
            return new BreakpointDirective(token, Name);
        }

        public override string ToString()
        {
            return $":breakpoint {Name}";
        }
    }

    public class MonitorDirective(Token token, string text) : Directive(token)
    {
        private readonly string text = text;

        public override MonitorDirective DeepCopy()
        {
            return new MonitorDirective(token, text);
        }

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

        public override RegisterAlias DeepCopy()
        {
            return new RegisterAlias(token, name, RegisterIndex);
        }

        public override string ToString()
        {
            return $":alias {Name} v{RegisterIndex:X}";
        }
    }

    public class ConstantAlias(Token token, string name, CalculationExpression expr) : Alias(token, name)
    {
        public CalculationExpression Expression { get; } = expr;

        public override ConstantAlias DeepCopy()
        {
            return new ConstantAlias(token, name, Expression.DeepCopy() as CalculationExpression);
        }

        public override string ToString()
        {
            return $":alias {Name} {Expression}";
        }
    }

    public class LabelDeclaration(Token token, string name) : Directive(token)
    {
        public string Name { get; } = name;

        public override LabelDeclaration DeepCopy()
        {
            return new LabelDeclaration(token, Name);
        }

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

        public override FunctionCallByName DeepCopy()
        {
            return new FunctionCallByName(token, Name.DeepCopy() as NamedReference);
        }

        public override string ToString()
        {
            return $":call {Name}";
        }
    }

    public class FunctionCallByNumber(Token token, CalculationExpression expr) : FunctionCall(token)
    {
        public CalculationExpression Expression { get; } = expr;

        public override FunctionCallByNumber DeepCopy()
        {
            return new FunctionCallByNumber(token, Expression.DeepCopy() as CalculationExpression);
        }

        public override string ToString()
        {
            return $":call {Expression}";
        }
    }

    public class ConstantDirective(Token token, string name, RValue value) : Directive(token)
    {
        public string Name { get; } = name;
        public RValue Value { get; } = value;

        public override ConstantDirective DeepCopy()
        {
            return new ConstantDirective(token, Name, Value.DeepCopy() as RValue);
        }

        public override string ToString()
        {
            return $":const {Name} {Value}";
        }
    }

    public abstract class UnpackDirective(Token token, NamedReference name) : Directive(token)
    {
        public NamedReference Name { get; } = name;
        public RValue ResolvedName { get; protected set; }

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

        public override UnpackNumberDirective DeepCopy()
        {
            UnpackNumberDirective und =  new(token, Name, Value.DeepCopy() as RValue);
            if (ResolvedName != null)
            {
                und.ResolvedName = ResolvedName.DeepCopy() as RValue;
            }
            return und;
        }

        public override string ToString()
        {
            return $":unpack {Value} {ResolvedName?.ToString() ?? Name.ToString()}";
        }
    }

    public class UnpackLongDirective(Token token, NamedReference name) : UnpackDirective(token, name)
    {
        public override UnpackLongDirective DeepCopy()
        {
            UnpackLongDirective uld = new(token, Name);
            if (ResolvedName != null)
            {
                uld.ResolvedName = ResolvedName.DeepCopy() as RValue;
            }
            return uld;
        }

        public override string ToString()
        {
            return $":unpack long {ResolvedName?.ToString() ?? Name.ToString()}";
        }
    }

    public class NextDirective(Token token, string label, Statement s) : Directive(token)
    {
        public string Label { get; } = label;
        public Statement Statement { get; } = s;

        public override NextDirective DeepCopy()
        {
            return new NextDirective(token, Label, Statement.DeepCopy() as Statement);
        }

        public override string ToString()
        {
            return $":next {Label} {Statement}";
        }
    }

    public class OrgDirective(Token token, CalculationExpression expr) : Directive(token)
    {
        public CalculationExpression Expression { get; } = expr;

        public override OrgDirective DeepCopy()
        {
            return new OrgDirective(token, Expression.DeepCopy() as CalculationExpression);
        }

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

        public override MacroDefinition DeepCopy()
        {
            string[] argsClone = new string[Arguments.Length];
            Statement[] bodyClone = new Statement[Body.Length];
            Array.Copy(Arguments, argsClone, Arguments.Length);
            for (int i = 0; i< Body.Length; i++)
            {
                bodyClone[i] = Body[i].DeepCopy() as Statement;
            }
            return new MacroDefinition(token, Name, argsClone, bodyClone);
        }

        public override string ToString()
        {
            return $"{Name} {string.Join(" ", Arguments)} {{...}}";
        }
    }

    public class Calculation(Token token, string name, CalculationExpression expr) : Directive(token)
    {
        public string Name { get; } = name;
        public CalculationExpression Expression { get; } = expr;

        public override Calculation DeepCopy()
        {
            return new Calculation(token, Name, Expression.DeepCopy() as CalculationExpression);
        }
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

        public override ValueByteDirective DeepCopy()
        {
            return new ValueByteDirective(token, Value.DeepCopy() as RValue);
        }

        public override string ToString()
        {
            return $":byte {Value}";
        }
    }

    public class ExpressionByteDirective(Token token, CalculationExpression expr) : ByteDirective(token)
    {
        public CalculationExpression Expression { get; } = expr;

        public override ExpressionByteDirective DeepCopy()
        {
            return new ExpressionByteDirective(token, Expression.DeepCopy() as CalculationExpression);
        }

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

        public override NamedPointerDirective DeepCopy()
        {
            NamedPointerDirective npd = new(token, Name.DeepCopy() as NamedReference);
            if (ResolvedValue != null)
            {
                npd.ResolvedValue = ResolvedValue.DeepCopy() as RValue;
            }
            return npd;
        }

        public override string ToString()
        {
            return $":pointer {ResolvedValue?.ToString() ?? Name.ToString()}";
        }
    }

    public class PointerExpressionDirective(Token token, CalculationExpression expr) : PointerDirective(token)
    {
        public CalculationExpression Expression { get; } = expr;

        public override PointerExpressionDirective DeepCopy()
        {
            return new PointerExpressionDirective(token, Expression.DeepCopy() as CalculationExpression);
        }

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

        public override StringDirective DeepCopy()
        {
            Statement[] bodyClone = new Statement[Body.Length];
            for (int i = 0; i < bodyClone.Length; i++)
            {
                bodyClone[i] = Body[i].DeepCopy() as Statement;
            }
            return new StringDirective(token, Name, Alphabet, bodyClone);
        }

        public override string ToString()
        {
            return $":stringmode {Name} \"{Alphabet}\" {{...}}";
        }
    }

    public class AssertDirective(Token token, CalculationExpression expr, string? msg = null) : Directive(token)
    {
        public CalculationExpression Expression { get; } = expr;
        public string? Message { get; } = msg;

        public override AssertDirective DeepCopy()
        {
            return new AssertDirective(token, Expression.DeepCopy() as CalculationExpression, Message);
        }

        public override string ToString()
        {
            return $":assert {Message} {Expression}";
        }
    }
    #endregion

    #region conditional expression
    public abstract class ConditionalExpression(Token token, RValue reg) : AstNode(token)
    {
        public RValue LeftHandOperand { get; private set; } = reg;

        public void ResolveLeft(RValue value)
        {
            LeftHandOperand = value;
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

        public override BinaryConditionalExpression DeepCopy()
        {
            return new BinaryConditionalExpression(token,
                LeftHandOperand.DeepCopy() as RValue,
                Operator,
                RightHandOperand.DeepCopy() as RValue);
        }

        public override string ToString()
        {
            return $"{LeftHandOperand} {operatorText[Operator]} {RightHandOperand}";
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

        public override KeystrokeCondition DeepCopy()
        {
            return new KeystrokeCondition(token, LeftHandOperand.DeepCopy() as RValue, KeyPressed);
        }

        public override string ToString()
        {
            return $"{LeftHandOperand} {(KeyPressed ? "" : "-")}key";
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

        public override IfStatement DeepCopy()
        {
            return new IfStatement(token, Condition.DeepCopy() as ConditionalExpression, Body?.DeepCopy() as Statement);
        }

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

        public override IfElseBlock DeepCopy()
        {
            Statement[] thenClone = new Statement[ThenBody.Length];
            Statement[] elseClone = new Statement[ElseBody.Length];
            for (int i = 0; i < ThenBody.Length; i++)
            {
                thenClone[i] = ThenBody[i].DeepCopy() as Statement;
            }
            for (int i = 0; i < ElseBody.Length; i++)
            {
                elseClone[i] = ElseBody[i].DeepCopy() as Statement;
            }
            return new IfElseBlock(token, Condition.DeepCopy() as ConditionalExpression, thenClone, elseClone);
        }

        public override string ToString()
        {
            return $"if {Condition} begin ...{(ElseBody.Length > 0 ? " else ..." : "")} end";
        }
    }

    public class LoopStatement(Token token, Statement[] body) : ControlFlowStatement(token)
    {
        public Statement[] Body { get; } = body;

        public override LoopStatement DeepCopy()
        {
            Statement[] bodyClone = new Statement[Body.Length];
            for (int i = 0; i < Body.Length; i++)
            {
                bodyClone[i] = Body[i].DeepCopy() as Statement;
            }
            return new LoopStatement(token, bodyClone);
        }

        public override string ToString()
        {
            return $"loop {(Body.Length > 0 ? "... " : "")}again";
        }
    }

    public class WhileStatement(Token token, ConditionalExpression expr, Statement s) : ControlFlowStatement(token)
    {
        public ConditionalExpression Condition { get; } = expr;
        public Statement Statement { get; } = s;

        public override WhileStatement DeepCopy()
        {
            return new WhileStatement(token,
                Condition.DeepCopy() as ConditionalExpression,
                Statement.DeepCopy() as Statement);
        }

        public override string ToString()
        {
            return $"while {Condition} {Statement}";
        }
    }
    #endregion

    #region calculations
    public abstract class CalculationExpression(Token token) : AstNode(token)
    {
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

        public override CalculationBinaryOperation DeepCopy()
        {
            return new CalculationBinaryOperation(token,
                Left.DeepCopy() as CalculationExpression,
                Right.DeepCopy() as CalculationExpression,
                Operator);
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

        public override CalculationUnaryOperation DeepCopy()
        {
            return new CalculationUnaryOperation(token, Expression.DeepCopy() as CalculationExpression, Operator);
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

        public override CalculationStringLengthOperation DeepCopy()
        {
            if (Text != null)
            {
                return new CalculationStringLengthOperation(token, Text, true);
            }
            return new CalculationStringLengthOperation(token, Name, false);
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

    public class CalculationName(Token token, NamedReference name) : CalculationExpression(token)
    {
        public NamedReference Name { get; } = name;
        public RValue ResolvedValue { get; private set; }

        public void Resolve(RValue value)
        {
            ResolvedValue = value;
        }

        public override CalculationName DeepCopy()
        {
            CalculationName cn = new(token, Name.DeepCopy() as NamedReference);
            if (ResolvedValue != null)
            {
                cn.ResolvedValue = ResolvedValue.DeepCopy() as RValue;
            }
            return cn;
        }

        public override string ToString()
        {
            return ResolvedValue.ToString() ?? Name.ToString();
        }
    }

    public class CalculationNumber(Token token, double num) : CalculationExpression(token)
    {
        public double Number { get; } = num;

        public override CalculationNumber DeepCopy()
        {
            return new CalculationNumber(token, Number);
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

        public override GenericRegisterReference DeepCopy()
        {
            GenericRegisterReference grr;
            if (Index.HasValue)
            {
                grr = new(FirstToken, Index.Value);
            }
            else
            {
                grr = new(FirstToken, Name.DeepCopy() as NamedReference);
            }
            if (Expression != null)
            {
                grr.Resolve(Expression.DeepCopy() as CalculationExpression);
            }
            return grr;
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

        public CalculationExpression? Expression { get; private set; }

        public bool Label { get; private set; } = false;

        public void ResolveToLabel() { Label = true; }
        public void ResolveToExpression(CalculationExpression expression) { Expression = expression; }

        public override NamedReference DeepCopy()
        {
            NamedReference @ref = new(token, Name);
            @ref.Label = Label;
            if (Expression != null)
            {
                @ref.Expression = Expression.DeepCopy() as CalculationExpression;
            }
            return @ref;
        }

        public override string ToString()
        {
            if (Label)
                return $"@{Name}";
            if (Expression != null)
                return $"[{Expression}]";
            return Name;
        }
    }

    public class NumericLiteral(Token token, double value) : RValue(token)
    {
        public double Value { get; } = value;

        public override NumericLiteral DeepCopy()
        {
            return new NumericLiteral(token, Value);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
