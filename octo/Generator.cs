using emulator;
using static octo.Analyzer;
using static octo.CalculationBinaryOperation;
using static octo.CalculationUnaryOperation;
using static octo.ImmediateAssignment;

namespace octo
{
    public class Generator(Statement[] statements)
    {
        private readonly Statement[] statements = statements;
        private readonly Dictionary <string, ushort> addresses = [];
        private ushort pos = 0;
        private ushort address = Chip8.PROGRAM_START_ADDRESS;
        private ushort unpack_hi = 0;
        private ushort unpack_lo = 1;
        private readonly ushort[] program = [];

        public ushort[] Generate()
        {
            foreach (Statement statement in statements)
            {
                GenerateUnknownStatement(statement);
            }

            return program;
        }

        private void GenerateUnknownStatement(Statement statement)
        {
            if (statement is Directive directive)
            {
                GenerateDirective(directive);
            }
            else if (statement is Assignment assignment)
            {
                GenerateAssignment(assignment);
            }
            else if (statement is ControlFlowStatement c)
            {
                GenerateControlFlowStatement(c);
            }
            else
            {
                GenerateStatement(statement);
            }
        }

        private void GenerateDirective(Directive directive)
        {
            if (directive is RegisterAlias ra)
            {
                if (ra.Name == "unpack-hi")
                {
                    unpack_hi = ra.RegisterIndex;
                }
                else if (ra.Name == "unpack-lo")
                {
                    unpack_lo = ra.RegisterIndex;
                }
            }
            else if (directive is ConstantAlias ca)
            {
                if (ca.Name == "unpack-hi")
                {
                    unpack_hi = GetNumber(ca.Expression, 4);
                }
                else if (ca.Name == "unpack-lo")
                {
                    unpack_lo = GetNumber(ca.Expression, 4);
                }
            }
            else if (directive is LabelDeclaration ld)
            {
                addresses[ld.Name] = address;
            }
            else if (directive is FunctionCallByName fcn)
            {
                GenerateInstruction("CALL", [GetNumber(fcn.Name, 12)]);
            }
            else if (directive is FunctionCallByNumber fcnum)
            {
                GenerateInstruction("CALL", [GetNumber(fcnum.Expression, 12)]);
            }
            else if (directive is UnpackDirective ud)
            {
                ushort value = GetNumber(ud.ResolvedName, 16);
                if (ud is UnpackNumberDirective und)
                {
                    value = (ushort)((GetNumber(und.Value, 4) << 12) | (value & 0xFFF));
                }
                byte hi = (byte)(value >> 8);
                byte lo = (byte)(value & 0xFF);
                GenerateInstruction("SETI", [unpack_lo, lo]);
                GenerateInstruction("SETI", [unpack_hi, hi]);
            }
            else if (directive is NextDirective nd)
            {
                addresses[nd.Label] = (ushort)(address + 1);
                GenerateUnknownStatement(nd.Statement);
            }
            else if (directive is OrgDirective od)
            {
                ushort target = GetNumber(od.Expression, 12);
                ushort roundedTarget = (ushort)(target & ~1);
                while (address < roundedTarget)
                {
                    GenerateData(0);
                }
                if (roundedTarget + 1 == address)
                {
                    GenerateByte(0);
                }
                if (address != target)
                {
                    throw new AnalysisException(od, $"unable to organize instructions at address 0x{target}");
                }
            }
            else if (directive is ValueByteDirective vbd)
            {
                byte value = (byte)GetNumber(vbd.Value, 8);
                GenerateByte(value);
            }
            else if (directive is ExpressionByteDirective ebd)
            {
                byte value = (byte)GetNumber(ebd.Expression, 8);
                GenerateByte(value);
            }
            else if (directive is NamedPointerDirective npd)
            {
                GenerateData(GetNumber(npd.ResolvedValue, 16));
            }
            else if (directive is PointerExpressionDirective ped)
            {
                GenerateData(GetNumber(ped.Expression, 16));
            }
            else if (directive is AssertDirective ad)
            {
                ushort result = GetNumber(ad.Expression, 16);
                if (result == 0)
                {
                    throw new AnalysisException(ad, $"\"{ad.Message}\": expression '{ad.Expression}' evaluated to '{result}'");
                }
            }
            else
            {
                throw new AnalysisException(directive, "unknown directive sub-type");
            }
        }

        private void GenerateAssignment(Assignment assignment)
        {
            if (assignment is LoadDelayAssignment lda)
            {
                GenerateInstruction("DLY", [GetRegisterIndex(lda.SourceRegister)]);
            }
            else if (assignment is SaveDelayAssignment sda)
            {
                GenerateInstruction("DCPY", [GetRegisterIndex(sda.DestinationRegister)]);
            }
            else if (assignment is LoadBuzzerAssignment lba)
            {
                GenerateInstruction("SND", [GetRegisterIndex(lba.SourceRegister)]);
            }
            else if (assignment is MemoryAssignment ma)
            {
                GenerateInstruction("STO", [GetNumber(ma.ResolvedValue, 12)]);
            }
            else if (assignment is MemoryIncrementAssignment mia)
            {
                GenerateInstruction("ADDM", [GetRegisterIndex(mia.Register)]);
            }
            else if (assignment is LoadCharacterAssignment lca)
            {
                GenerateInstruction("SPR", [GetRegisterIndex(lca.Register)]);
            }
            else if (assignment is KeyInputAssignment kia)
            {
                GenerateInstruction("INP", [GetRegisterIndex(kia.DestinationRegister)]);
            }
            else if (assignment is RegisterAssignment ra)
            {
                string mnemonic = ra.Operator switch
                {
                    RegisterAssignment.AssignmentOperator.Assignment => "SET",
                    RegisterAssignment.AssignmentOperator.Or => "OR",
                    RegisterAssignment.AssignmentOperator.And => "AND",
                    RegisterAssignment.AssignmentOperator.Xor => "XOR",
                    RegisterAssignment.AssignmentOperator.Addition => "ADD",
                    RegisterAssignment.AssignmentOperator.Subtraction => "SUB",
                    RegisterAssignment.AssignmentOperator.RightShift => "RSH",
                    RegisterAssignment.AssignmentOperator.ReverseSubtraction => "RSUB",
                    RegisterAssignment.AssignmentOperator.LeftShift => "LSH"
                };
                GenerateInstruction(mnemonic, [GetRegisterIndex(ra.DestinationRegister),
                                               GetRegisterIndex(ra.SourceRegister)]);
            }
            else if (assignment is ImmediateAssignment ia)
            {
                ushort[] args = [GetRegisterIndex(ia.DestinationRegister),
                                 GetNumber(ia.Value, 8)];
                if (ia.Op == Operator.Subtraction)
                {
                    args[1] = (ushort)((~args[1] + 1) & 0xFF);
                }
                string mnemonic = ia.Op == Operator.Assignment ? "SETI" : "ADDI";
                GenerateInstruction(mnemonic, args);
            }
            else if (assignment is RandomAssignment rna)
            {
                GenerateInstruction("RAND", [GetRegisterIndex(rna.DestinationRegister),
                                             GetNumber(rna.Mask, 8)]);
            }
            else
            {
                throw new AnalysisException(assignment, "unknown assignment sub-type");
            }
        }

        private void GenerateControlFlowStatement(ControlFlowStatement statement)
        {
            if (statement is IfStatement ifStatement)
            {
                bool dangling = ifStatement.Body == null;
            }
            else if (statement is IfElseBlock ifElseBlock)
            {

            }
            else if (statement is LoopStatement loopStatement)
            {
                ushort startOfLoop = address;
                foreach (Statement s in loopStatement.Body)
                {
                    GenerateUnknownStatement(s);
                }
                GenerateInstruction("JMPI", [startOfLoop]);
            }
            else if (statement is WhileStatement whileStatement)
            {

            }
            else
            {
                throw new AnalysisException(statement, "unknown control flow sub-type");
            }
        }

        private void GenerateCondition(ConditionalExpression expression)
        {
            if (expression is KeystrokeCondition kc)
            {
                GenerateInstruction(kc.KeyPressed ? "SKEQ" : "SKNE",
                                    [GetRegisterIndex(kc.LeftRegister)]);
            }
            else if (expression is BinaryConditionalExpression bce)
            {
                BinaryConditionalExpression.ConditionalOperator op = bce.Operator;
                // TODO
                // <, <=, >, >= can use "SUB", "RSUB", or "ADDI" (w/ negation)
                // if left is number, have to negate op and swap left and right operands
            }
            else
            {
                throw new AnalysisException(expression, "unknown conditional sub-type");
            }
        }

        private void GenerateStatement(Statement statement)
        {
            if (statement is ReturnStatement)
            {
                GenerateInstruction("RET", []);
            }
            else if (statement is ClearStatement)
            {
                GenerateInstruction("CLR", []);
            }
            else if (statement is BcdStatement bcds)
            {
                GenerateInstruction("BCD", [GetRegisterIndex(bcds.Register)]);
            }
            else if (statement is LoadStatement ls)
            {
                GenerateInstruction("LD", [GetRegisterIndex(ls.Register)]);
            }
            else if (statement is SaveStatement ss)
            {
                GenerateInstruction("SAVE", [GetRegisterIndex(ss.Register)]);
            }
            else if (statement is SpriteStatement sps)
            {
                ushort[] args = [GetRegisterIndex(sps.XRegister),
                                 GetRegisterIndex(sps.YRegister),
                                 GetNumber(sps.Height, 4)];
                GenerateInstruction("DRAW", args);
            }
            else if (statement is JumpStatement js)
            {
                GenerateInstruction(js.Immediate ? "JMPI" : "JMPA",
                                    [GetNumber(js.Target, 12)]);
            }
            else if (statement is DataDeclaration dd)
            {
                GenerateData(dd.Value);
            }
            else
            {
                throw new AnalysisException(statement, "unknown statement sub-type");
            }
        }

        private void GenerateInstruction(string mnemonic, ushort[] args)
        {
            Instruction instruction = new(mnemonic, args);
            GenerateData(instruction.Value);
        }

        private void GenerateData(ushort value)
        {
            program[pos] = value;
            pos++;
            address += 2;
        }

        private void GenerateByte(byte value)
        {
            // TODO must only emit in pairs
        }

        private byte GetRegisterIndex(RValue value)
        {
            if (value is GenericRegisterReference reg)
            {
                if (reg.Index.HasValue)
                {
                    return reg.Index.Value;
                }
                double d = Interpret(reg.Expression);
                ulong l = Convert.ToUInt64(d);
                byte b = (byte)(Convert.ToByte(l) & 0xF);
                if (b != d)
                {
                    throw new AnalysisException(reg.Expression, $"converting {d} to register index would result in truncation");
                }
                return b;
            }
            throw new AnalysisException(value, $"value is not resolved");
        }

        private ushort GetNumber(RValue value, int numBits)
        {
            ushort number;
            if (value is NumericLiteral n)
            {
                number = Convert.ToUInt16(n.Value);
                if (number != n.Value)
                {
                    throw new AnalysisException(n, $"converting {n.Value} to {numBits} bits would result in truncation");
                }
                // TODO verify num bits
            }
            else if (value is GenericRegisterReference reg)
            {
                return GetRegisterIndex(reg);
            }
            else if (value is NamedReference name)
            {
                // TODO
            }
            throw new AnalysisException(value, "unknown RValue sub-type");
        }

        private ushort GetNumber(CalculationExpression expression, int numBits)
        {
            // TODO
            throw new NotImplementedException();
        }

        private double Interpret(CalculationExpression expression)
        {
            if (expression is CalculationBinaryOperation bop)
            {
                double l = Interpret(bop.Left);
                double r = Interpret(bop.Right);
                return bop.Operator switch
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
            else if (expression is CalculationUnaryOperation uop)
            {
                if (uop.Operator == UnaryOperator.AddressOf)
                {
                    string name = (uop.Expression as CalculationName).Name.Name;
                    return addresses[name];
                }
                double e = Interpret(uop.Expression);
                return uop.Operator switch
                {
                    UnaryOperator.NumericalNegation => -e,
                    UnaryOperator.BitwiseNegation => ~Convert.ToUInt32(e),
                    UnaryOperator.LogicalNegation => Convert.ToDouble(!Convert.ToBoolean(e)),
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
            else if (expression is CalculationStringLengthOperation slop)
            {
                return slop.Text.Length;
            }
            else if (expression is CalculationNumber num)
            {
                return num.Number;
            }
            else if (expression is CalculationName name)
            {
                if (name.ResolvedValue is NumericLiteral n)
                {
                    return n.Value;
                }
                else if (name.ResolvedValue is NamedReference @ref)
                {
                    if (@ref.Label)
                    {
                        if (addresses.TryGetValue(name.Name.Name, out ushort value))
                        {
                            return value;
                        }

                        throw new AnalysisException(name, $"unknown label {name.Name.Name}");
                    }
                    else if (@ref.Value != null)
                    {
                        return @ref.Value.Value;
                    }
                    else if (@ref.Expression != null)
                    {
                        return Interpret(@ref.Expression);
                    }
                    else if (@ref.Constant)
                    {
                        return name.Name.Name switch
                        {
                            "E" => Math.E,
                            "PI" => Math.PI,
                            "HERE" => address + 2,
                        };
                    }
                }

                throw new AnalysisException(name, $"unresolved name {name.Name}");
            }
            else
            {
                throw new AnalysisException(expression, $"unknown calculation sub-type");
            }
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
}
