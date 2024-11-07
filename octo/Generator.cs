using emulator;
using System.Diagnostics;
using static octo.Analyzer;
using static octo.CalculationBinaryOperation;
using static octo.CalculationUnaryOperation;
using static octo.ImmediateAssignment;

namespace octo
{
    public class Generator(Statement[] statements)
    {
        private static readonly Dictionary<BinaryConditionalExpression.ConditionalOperator, BinaryConditionalExpression.ConditionalOperator> negations = new()
        {
            {BinaryConditionalExpression.ConditionalOperator.Equality, BinaryConditionalExpression.ConditionalOperator.Inequality },
            {BinaryConditionalExpression.ConditionalOperator.Inequality, BinaryConditionalExpression.ConditionalOperator.Equality },
            {BinaryConditionalExpression.ConditionalOperator.LessThan, BinaryConditionalExpression.ConditionalOperator.GreaterThanOrEqualTo },
            {BinaryConditionalExpression.ConditionalOperator.LessThanOrEqualTo, BinaryConditionalExpression.ConditionalOperator.GreaterThan },
            {BinaryConditionalExpression.ConditionalOperator.GreaterThan, BinaryConditionalExpression.ConditionalOperator.LessThanOrEqualTo },
            {BinaryConditionalExpression.ConditionalOperator.GreaterThanOrEqualTo, BinaryConditionalExpression.ConditionalOperator.LessThan },
        };
        private readonly Statement[] statements = statements;
        private readonly Dictionary <string, ushort> addresses = [];
        private readonly List<byte> program = [];
        private readonly Dictionary<string, HashSet<int>> backpatches = [];
        private readonly Stack<HashSet<int>> whileStack = [];
        private ushort pos = 0;
        private ushort address = Chip8.PROGRAM_START_ADDRESS;
        private byte unpack_hi = 0;
        private byte unpack_lo = 1;

        public byte[] Generate()
        {
            GenerateInstruction("JMPI", [0x0]);
            CreateBackpatch("main", 0);
            foreach (Statement statement in statements)
            {
                GenerateUnknownStatement(statement);
            }

            return program.ToArray();
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
                    unpack_hi = GetByte(ca.Expression, 4);
                }
                else if (ca.Name == "unpack-lo")
                {
                    unpack_lo = GetByte(ca.Expression, 4);
                }
            }
            else if (directive is LabelDeclaration ld)
            {
                Trace.WriteLine($"generating label {ld.Name}");
                addresses[ld.Name] = address;
                if (backpatches.TryGetValue(ld.Name, out HashSet<int> set))
                {
                    foreach (int i in set)
                    {
                        Backpatch(address, i);
                    }
                }
            }
            else if (directive is FunctionCallByName fcn)
            {
                GenerateInstruction("CALL", [GetShort(fcn.Name, 12)]);
            }
            else if (directive is FunctionCallByNumber fcnum)
            {
                GenerateInstruction("CALL", [GetShort(fcnum.Expression, 12)]);
            }
            else if (directive is UnpackDirective ud)
            {
                ushort value = GetShort(ud.ResolvedName, 16);
                if (ud is UnpackNumberDirective und)
                {
                    value = (ushort)((GetByte(und.Value, 4) << 12) | (value & 0xFFF));
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
                ushort target = GetShort(od.Expression, 12);
                ushort roundedTarget = (ushort)(target & ~1);
                if (address % 2 == 1)
                {
                    GenerateByte(0);
                }
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
                byte value = GetByte(vbd.Value, 8);
                GenerateByte(value);
            }
            else if (directive is ExpressionByteDirective ebd)
            {
                byte value = GetByte(ebd.Expression, 8);
                GenerateByte(value);
            }
            else if (directive is NamedPointerDirective npd)
            {
                GenerateData(GetShort(npd.ResolvedValue, 16));
            }
            else if (directive is PointerExpressionDirective ped)
            {
                GenerateData(GetShort(ped.Expression, 16));
            }
            else if (directive is AssertDirective ad)
            {
                ushort result = GetShort(ad.Expression, 16);
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
                GenerateInstruction("STO", [GetShort(ma.ResolvedValue, 12)]);
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
                                 GetByte(ia.Value, 8)];
                if (ia.Op == Operator.Subtraction)
                {
                    args[1] = Negate(args[1]);
                }
                string mnemonic = ia.Op == Operator.Assignment ? "SETI" : "ADDI";
                GenerateInstruction(mnemonic, args);
            }
            else if (assignment is RandomAssignment rna)
            {
                GenerateInstruction("RAND", [GetRegisterIndex(rna.DestinationRegister),
                                             GetByte(rna.Mask, 8)]);
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
                GenerateCondition(ifStatement.Condition, false);
                if (ifStatement.Body != null)
                {
                    GenerateUnknownStatement(ifStatement.Body);
                }
            }
            else if (statement is IfElseBlock ifElseBlock)
            {
                GenerateCondition(ifElseBlock.Condition, true);
                int start_pos = pos;
                GenerateInstruction("JMPI", [0x0]);
                foreach (Statement inner in ifElseBlock.ThenBody)
                {
                    GenerateUnknownStatement(inner);
                }
                int end_of_if_pos = pos;
                GenerateInstruction("JMPI", [0x0]);
                Backpatch(address, start_pos);
                foreach (Statement inner in ifElseBlock.ElseBody)
                {
                    GenerateUnknownStatement(inner);
                }
                Backpatch(address, end_of_if_pos);
            }
            else if (statement is LoopStatement loopStatement)
            {
                ushort startOfLoop = address;
                whileStack.Push([]);
                foreach (Statement s in loopStatement.Body)
                {
                    GenerateUnknownStatement(s);
                }
                GenerateInstruction("JMPI", [startOfLoop]);
                foreach (int p in whileStack.Pop())
                {
                    Backpatch(address, p);
                }
            }
            else if (statement is WhileStatement whileStatement)
            {
                GenerateCondition(whileStatement.Condition, true);
                whileStack.Peek().Add(pos);
                GenerateInstruction("JMPI", [0x0]);
            }
            else
            {
                throw new AnalysisException(statement, "unknown control flow sub-type");
            }
        }

        private void GenerateCondition(ConditionalExpression expression, bool negate)
        {
            if (expression is KeystrokeCondition kc)
            {
                GenerateInstruction(kc.KeyPressed ? "SKEQ" : "SKNE",
                                    [GetRegisterIndex(kc.LeftHandOperand)]);
            }
            else if (expression is BinaryConditionalExpression bce)
            {
                BinaryConditionalExpression.ConditionalOperator op = bce.Operator;
                if (negate)
                {
                    op = negations[op];
                }
                bool leftIsReg = bce.LeftHandOperand is GenericRegisterReference;
                bool rightIsReg = bce.RightHandOperand is GenericRegisterReference;

                ushort[] args;
                if (!leftIsReg && rightIsReg)
                {
                    args = [GetRegisterIndex(bce.RightHandOperand),
                            GetByte(bce.LeftHandOperand, 8)];
                }
                else
                {
                    args = [GetRegisterIndex(bce.LeftHandOperand),
                            (rightIsReg ? GetRegisterIndex(bce.RightHandOperand) :
                                          GetByte(bce.RightHandOperand, 8))];
                }

                if (leftIsReg ^ rightIsReg)
                {
                    if (op == BinaryConditionalExpression.ConditionalOperator.Equality)
                    {
                        GenerateInstruction("SNEI", args);
                    }
                    else if (op == BinaryConditionalExpression.ConditionalOperator.Inequality)
                    {
                        GenerateInstruction("SEQI", args);
                    }
                    else
                    {
                        GenerateInstruction("SETI", [0xF, args[1]]);
                    }
                }
                else
                {
                    if (op == BinaryConditionalExpression.ConditionalOperator.Equality)
                    {
                        GenerateInstruction("SNE", args);
                    }
                    else if (op == BinaryConditionalExpression.ConditionalOperator.Inequality)
                    {
                        GenerateInstruction("SEQ", args);
                    }
                    else
                    {
                        GenerateInstruction("SET", [0xF, args[1]]);
                    }
                }

                if (op != BinaryConditionalExpression.ConditionalOperator.Equality &&
                    op != BinaryConditionalExpression.ConditionalOperator.Inequality)
                {
                    if (op == BinaryConditionalExpression.ConditionalOperator.GreaterThan ||
                        op == BinaryConditionalExpression.ConditionalOperator.LessThanOrEqualTo)
                    {
                        GenerateInstruction("SUB", [0xF, args[0]]);
                    }
                    else
                    {
                        GenerateInstruction("RSUB", [0xF, args[0]]);
                    }

                    if (op == BinaryConditionalExpression.ConditionalOperator.LessThan ||
                        op == BinaryConditionalExpression.ConditionalOperator.GreaterThan)
                    {
                        GenerateInstruction("SNEI", [0xF, 0]);
                    }
                    else
                    {
                        GenerateInstruction("SEQI", [0xF, 0]);
                    }
                }
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
                                 GetByte(sps.Height, 4)];
                GenerateInstruction("DRAW", args);
            }
            else if (statement is JumpStatement js)
            {
                GenerateInstruction(js.Immediate ? "JMPI" : "JMPA",
                                    [GetShort(js.Target, 12)]);
            }
            else if (statement is DataDeclaration dd)
            {
                GenerateByte(dd.Value);
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
            GenerateByte((byte)(value >> 8));
            GenerateByte((byte)(value & 0xFF));
        }

        private void GenerateByte(byte value)
        {
            program.Add(value);
            pos++;
            address++;
        }

        private void Backpatch(ushort address, int pos)
        {
            Trace.WriteLine($"backpatching address 0x{address:X} at {pos}");
            ushort value = (ushort)((program[pos] << 8) | program[pos + 1]);
            if (address > 0x0fff)
            {
                throw new AnalysisException("target resolved to greater than 12 bits");
            }
            if ((value & 0xF000) == 0x6000)
            {
                byte x = (byte)((value >> 8) & 0x000F);
                if (x == unpack_hi)
                {
                    program[pos + 1] = (byte)(address >> 8);
                }
                else if (x == unpack_lo)
                {
                    program[pos + 1] = (byte)(address & 0xFF);
                }
                else
                {
                    throw new AnalysisException($"cannot backpatch a SETI that isn't an unpack");
                }
                
            }
            else if (value != 0x1000 &&
                     value != 0x2000 &&
                     value != 0xA000 &&
                     value != 0xB000)
            {
                throw new AnalysisException($"cannot backpatch instruction 0x{value:x}");
            }
            program[pos] |= (byte)((address & 0x0F00) >> 8);
            program[pos + 1] = (byte)(address & 0x00FF);
        }

        private void CreateBackpatch(string name, int pos)
        {
            Trace.WriteLine($"creating backpatch for {name} at {pos}");
            if (backpatches.TryGetValue(name, out HashSet<int> set))
            {
                set.Add(pos);
            }
            else
            {
                backpatches.Add(name, [pos]);
            }
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

        private ushort GetShort(RValue value, int numBits)
        {
            if (numBits > 16)
            {
                throw new ArgumentException("must be at most 16", nameof(numBits));
            }
            ushort number;
            if (value is NumericLiteral n)
            {
                try
                {
                    if (n.Value < 0)
                    {
                        number = Negate(Convert.ToUInt16(-n.Value));
                    }
                    else
                    {
                        number = Convert.ToUInt16(n.Value);
                    }
                }
                catch
                {
                    throw new AnalysisException(value, $"cannot convert {n.Value} to integer");
                }
                if (number != n.Value)
                {
                   // throw new AnalysisException(value, $"expected {numBits}-bit unsigned integer");
                }
            }
            else if (value is GenericRegisterReference reg)
            {
                return GetRegisterIndex(reg);
            }
            else if (value is NamedReference name)
            {
                if (name.Label)
                {
                    if (!addresses.TryGetValue(name.Name, out number))
                    {
                        number = 0;
                        CreateBackpatch(name.Name, pos);
                    }
                }
                else if (name.Expression != null)
                {
                    return GetShort(name.Expression, numBits);
                }
                else throw new AnalysisException(value, "was never resolved");
            }
            else throw new AnalysisException(value, "unknown RValue sub-type");

            ushort masked = (ushort)(number & ((1 << numBits) - 1));
            if (number != masked)
            {
                // throw new AnalysisException(value, $"expected at most {numBits}-bit value");
            }
            return masked;
        }

        private byte GetByte(RValue value, int numBits)
        {
            if (numBits > 8)
            {
                throw new ArgumentException("must be at most 8", nameof(numBits));
            }
            ushort number = GetShort(value, numBits);
            byte masked = (byte)(number & ((1 << numBits) - 1));
            if (number != masked)
            {
                throw new AnalysisException(value, $"expected at most {numBits}-bit value");
            }
            return masked;
        }

        private ushort GetShort(CalculationExpression expression, int numBits)
        {
            if (numBits > 16)
            {
                throw new ArgumentException("must be at most 16", nameof(numBits));
            }
            double number = Interpret(expression);
            ushort converted = Convert.ToUInt16(number);
            ushort masked = (ushort)(converted & ((1 << numBits) - 1));
            return masked;
        }

        private byte GetByte(CalculationExpression expression, int numBits)
        {
            if (numBits > 8)
            {
                throw new ArgumentException("must be at most 8", nameof(numBits));
            }
            ushort number = GetShort(expression, numBits);
            byte masked = (byte)(number & ((1 << numBits) - 1));
            return masked;
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
                    else if (@ref.Expression != null)
                    {
                        return Interpret(@ref.Expression);
                    }
                    else
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

        private static ushort Negate(ushort n)
        {
            return (ushort)((~n + 1) & 0xFFFF);
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
