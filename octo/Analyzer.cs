﻿using System.Text.RegularExpressions;

namespace octo
{
    public class Analyzer(Statement[] statements)
    {
        private readonly Statement[] statements = statements;
        private readonly List<Statement> analyzedStatements = [];
        private readonly Dictionary<string, byte> registerAliases = [];
        private readonly Dictionary<string, CalculationExpression> registerConstants = [];
        private readonly Dictionary<string, RValue> constants = [];
        private readonly HashSet<string> labels = [];
        private readonly Dictionary<string, CalculationExpression> calcs = [];
        private readonly Dictionary<string, MacroDefinition> macroNames = [];
        private readonly Dictionary<Directive, uint> macroCalls = [];
        private readonly LinkedList<Dictionary<string, string>> macroSubstitutions = [];
        private readonly Dictionary<string, Dictionary<string, StringDirective>> stringDirectives = [];
        private readonly HashSet<string> breakpoints = [];
        private string? currentMacro = null;
        private string? currentStringMode = null;
        private bool inCalculation = false;

        public Statement[] Analyze()
        {
            CollectLabels();

            foreach (Statement statement in statements)
            {
                AnalyzeUnknownStatement(statement);
            }

            if (!labels.Contains("main"))
            {
                throw new AnalysisException(statements[0], "program must contain a 'main' label");
            }

            return analyzedStatements.ToArray();
        }

        private void CollectLabels()
        {
            foreach (Statement statement in statements)
            {
                if (statement is LabelDeclaration ld)
                {
                    if (labels.Contains(ld.Name))
                    {
                        throw new AnalysisException(ld, $"label '{ld.Name}' already declared");
                    }
                    else
                    {
                        labels.Add(ld.Name);
                    }
                }
                else if (statement is NextDirective nd)
                {
                    if (labels.Contains(nd.Label))
                    {
                        throw new AnalysisException(nd, $"label '{nd.Label}' already declared");
                    }
                    else
                    {
                        labels.Add(nd.Label);
                    }
                }
            }
        }

        private Statement AnalyzeUnknownStatement(Statement statement, bool emit = true)
        {
            if (statement is Directive d)
            {
                return AnalyzeDirective(d, emit);
            }
            else if (statement is Assignment a)
            {
                return AnalyzeAssignment(a, emit);
            }
            else if (statement is ControlFlowStatement c)
            {
                return AnalyzeControlFlowStatement(c, emit);
            }
            else
            {
                return AnalyzeStatement(statement, emit);
            }
        }
 
        private Directive AnalyzeDirective(Directive directive, bool emit)
        {
            bool reuse = true;
            if (directive is BreakpointDirective dd)
            {
                if (breakpoints.Contains(dd.Name))
                {
                    throw new AnalysisException(dd, $"breakpoint with name '{dd.Name}' already declared");
                }
                breakpoints.Add(dd.Name);
                reuse = false;
            }
            else if (directive is MonitorDirective)
            {
                reuse = false;
            }
            else if (directive is RegisterAlias ra)
            {
                VerifyNameUnique(ra, ra.Name);
                registerAliases[ra.Name] = ra.RegisterIndex;
                reuse = false;
            }
            else if (directive is ConstantAlias ca)
            {
                VerifyNameUnique(ca, ca.Name);
                registerConstants[ca.Name] = ca.Expression;
                reuse = false;
            }
            else if (directive is LabelDeclaration ld)
            {
                VerifyNameUnique(ld, ld.Name, allowLabelDuplicates:true);
            }
            else if (directive is FunctionCallByName fc)
            {
                VerifyNameExistsInContext(fc.Name, false);
            }
            else if (directive is FunctionCallByNumber fcn)
            {
                AnalyzeCalculation(fcn.Expression);
            }
            else if (directive is ConstantDirective cd)
            {
                VerifyNameUnique(cd, cd.Name);
                VerifyNameExistsInContext(cd.Value, true);
                constants[cd.Name] = cd.Value;
                reuse = false;
            }
            else if (directive is UnpackDirective ud)
            {
                ud.ResolveName(ResolveNamedReference(ud.Name, true));

                if (ud is UnpackNumberDirective und)
                {
                    VerifyRValue(und.Value, false, true);
                    und.Resolve(ResolveRValue(und.Value, true));
                }
            }
            else if (directive is NextDirective nd)
            {
                VerifyNameUnique(nd, nd.Label, allowLabelDuplicates:true);
                nd.Statement = AnalyzeUnknownStatement(nd.Statement, false);
            }
            else if (directive is OrgDirective od)
            {
                AnalyzeCalculation(od.Expression);
            }
            else if (directive is MacroDefinition md)
            {
                VerifyNameUnique(md, md.Name);
                macroNames[md.Name] = md;
                macroCalls[md] = 0;
                reuse = false;
            }
            else if (directive is Calculation calc)
            {
                VerifyNameUnique(calc, calc.Name);

                AnalyzeCalculation(calc.Expression);

                // add entry after analysis so self-reference is not allowed
                calcs[calc.Name] = calc.Expression;
                reuse = false;
            }
            else if (directive is ValueByteDirective vbd)
            {
                VerifyRValue(vbd.Value, false, true);
                vbd.Resolve(ResolveRValue(vbd.Value, true));
            }
            else if (directive is ExpressionByteDirective ebd)
            {
                AnalyzeCalculation(ebd.Expression);
            }
            else if (directive is NamedPointerDirective npd)
            {
                npd.Resolve(ResolveNamedReference(npd.Name, true));
            }
            else if (directive is PointerExpressionDirective ped)
            {
                AnalyzeCalculation(ped.Expression);
            }
            else if (directive is StringDirective sd)
            {
                VerifyNameUnique(sd, sd.Name, allowStringModeDuplicates:true);
                if (stringDirectives.TryGetValue(sd.Name, out Dictionary<string, StringDirective> byAlphabet))
                {
                    if (byAlphabet.Keys.Any(a => a.Any(c => sd.Alphabet.Contains(c))))
                    {
                        throw new AnalysisException(sd, $"string-mode directive '{sd.Name},' with alphabet '{sd.Alphabet}', " +
                            $"overlaps alphabet of existing string-mode directive with same name");
                    }
                    byAlphabet[sd.Alphabet] = sd;
                }
                else
                {
                    stringDirectives[sd.Name] = new Dictionary<string, StringDirective>() { { sd.Alphabet, sd } };
                }
                macroCalls[sd] = 0;
                reuse = false;
            }
            else if (directive is AssertDirective ad)
            {
                AnalyzeCalculation(ad.Expression);
            }
            else
            {
                throw new AnalysisException(directive, "unknown directive sub-type");
            }

            if (reuse && emit)
            {
                analyzedStatements.Add(directive);
            }

            return directive;
        }

        private Assignment AnalyzeAssignment(Assignment assignment, bool emit)
        {
            bool reuse = true;
            GenericRegisterReference tmp;
            if (assignment is LoadDelayAssignment lda)
            {
                VerifyVRegister(lda.SourceRegister);
                tmp = lda.SourceRegister;
                ResolveRegisterReference(ref tmp);
                lda.Resolve(tmp);
            }
            else if (assignment is SaveDelayAssignment sda)
            {
                VerifyVRegister(sda.DestinationRegister);
                tmp = sda.DestinationRegister;
                ResolveRegisterReference(ref tmp);
                sda.Resolve(tmp);
            }
            else if (assignment is LoadBuzzerAssignment lba)
            {
                VerifyVRegister(lba.SourceRegister);
                tmp = lba.SourceRegister;
                ResolveRegisterReference(ref tmp);
                lba.Resolve(tmp);
            }
            else if (assignment is MemoryAssignment ma)
            {
                if (ma.Address != null)
                {
                    VerifyNumber(ma.Address, 12);
                    ma.Resolve(ma.Address);
                }
                else
                {
                    VerifyNameExistsInContext(ma.Name, true);
                    NamedReference nr = ma.Name;
                    ma.Resolve(ResolveNamedReference(nr, true));
                }
            }
            else if (assignment is MemoryIncrementAssignment mia)
            {
                VerifyVRegister(mia.Register);
                tmp = mia.Register;
                ResolveRegisterReference(ref tmp);
                mia.Resolve(tmp);
            }
            else if (assignment is LoadCharacterAssignment lca)
            {
                VerifyVRegister(lca.Register);
                tmp = lca.Register;
                ResolveRegisterReference(ref tmp);
                lca.Resolve(tmp);
            }
            else if (assignment is KeyInputAssignment kia)
            {
                VerifyVRegister(kia.DestinationRegister);
                tmp = kia.DestinationRegister;
                ResolveRegisterReference(ref tmp);
                kia.Resolve(tmp);
            }
            else if (assignment is RegisterAssignment ra)
            {
                VerifyVRegister(ra.DestinationRegister);
                tmp = ra.DestinationRegister;
                ResolveRegisterReference(ref tmp);
                ra.ResolveDestination(tmp);

                VerifyVRegister(ra.SourceRegister);
                tmp = ra.SourceRegister;
                ResolveRegisterReference(ref tmp);
                ra.ResolveSource(tmp);
            }
            else if (assignment is ImmediateAssignment ia)
            {
                VerifyVRegister(ia.DestinationRegister);
                tmp = ia.DestinationRegister;
                ResolveRegisterReference(ref tmp);
                ia.ResolveDestination(tmp);

                VerifyRValue(ia.Value, false, true);
                ia.ResolveArgument(ResolveRValue(ia.Value, true));
            }
            else if (assignment is RandomAssignment rna)
            {
                VerifyVRegister(rna.DestinationRegister);
                tmp = rna.DestinationRegister;
                ResolveRegisterReference(ref tmp);
                rna.ResolveRegister(tmp);

                VerifyRValue(rna.Mask, false, true);
                rna.ResolveMask(ResolveRValue(rna.Mask, true));
            }
            else if (assignment is AmbiguousAssignment aa)
            {
                reuse = false;
                Assignment disambiguatedAssignment;
                VerifyVRegister(aa.DestinationRegister);
                tmp = aa.DestinationRegister;
                ResolveRegisterReference(ref tmp);
                if (IsVRegister(aa.Name))
                {
                    GenericRegisterReference reg = new(aa.Name.FirstToken, aa.Name);
                    ResolveRegisterReference(ref reg);
                    disambiguatedAssignment = aa.Op switch
                    {
                        ImmediateAssignment.Operator.Assignment => new RegisterCopyAssignment(aa.DestinationRegister.FirstToken, aa.DestinationRegister, reg),
                        ImmediateAssignment.Operator.Addition => new RegisterAddAssignment(aa.DestinationRegister.FirstToken, aa.DestinationRegister, reg),
                        ImmediateAssignment.Operator.Subtraction => new RegisterSubtractionAssignment(aa.DestinationRegister.FirstToken, aa.DestinationRegister, reg),
                    };
                    ((RegisterAssignment)disambiguatedAssignment).ResolveSource(reg);
                    ((RegisterAssignment)disambiguatedAssignment).ResolveDestination(tmp);
                }
                else
                {
                    RValue value = ResolveNamedReference(aa.Name, true);
                    disambiguatedAssignment = aa.Op switch
                    {
                        ImmediateAssignment.Operator.Assignment => new ImmediateAssignment(aa.DestinationRegister.FirstToken, aa.DestinationRegister, value),
                        ImmediateAssignment.Operator.Addition => new ImmediateAdditionAssignment(aa.DestinationRegister.FirstToken, aa.DestinationRegister, value),
                        ImmediateAssignment.Operator.Subtraction => new ImmediateSubtractionAssignment(aa.DestinationRegister.FirstToken, aa.DestinationRegister, value),
                    };
                    ((ImmediateAssignment)disambiguatedAssignment).ResolveDestination(tmp);
                    ((ImmediateAssignment)disambiguatedAssignment).ResolveArgument(value);
                }

                if (emit)
                {
                    analyzedStatements.Add(disambiguatedAssignment);
                }

                return disambiguatedAssignment;
            }
            else
            {
                throw new AnalysisException(assignment, "unknown assignment sub-type");
            }
            
            if (reuse && emit)
            {
                analyzedStatements.Add(assignment);
            }

            return assignment;
        }

        private Statement AnalyzeStatement(Statement statement, bool emit)
        {
            bool reuse = true;
            GenericRegisterReference tmp;
            if (statement is ReturnStatement)
            {

            }
            else if (statement is ClearStatement)
            {

            }
            else if (statement is BcdStatement bcds)
            {
                VerifyVRegister(bcds.Register);
                tmp = bcds.Register;
                ResolveRegisterReference(ref tmp);
                bcds.Resolve(tmp);
            }
            else if (statement is LoadStatement ls)
            {
                VerifyVRegister(ls.Register);
                tmp = ls.Register;
                ResolveRegisterReference(ref tmp);
                ls.Resolve(tmp);
            }
            else if (statement is SaveStatement ss)
            {
                VerifyVRegister(ss.Register);
                tmp = ss.Register;
                ResolveRegisterReference(ref tmp);
                ss.Resolve(tmp);
            }
            else if (statement is SpriteStatement sps)
            {
                VerifyVRegister(sps.XRegister);
                VerifyVRegister(sps.YRegister);
                VerifyRValue(sps.Height, false, true);
                tmp = sps.XRegister;
                ResolveRegisterReference(ref tmp);
                sps.ResolveX(tmp);

                tmp = sps.YRegister;
                ResolveRegisterReference(ref tmp);
                sps.ResolveY(tmp);

                sps.ResolveHeight(ResolveRValue(sps.Height, true));
            }
            else if (statement is JumpStatement js)
            {
                if (js.Target != null)
                {
                    VerifyRValue(js.Target, false, false);
                    ResolveRValue(js.Target, false);
                }
            }
            else if (statement is MacroInvocation mi)
            {
                reuse = false;
                VerifyMacro(mi, mi.MacroName, mi.Arguments, emit);
            }
            else if (statement is AmbiguousCall ac)
            {
                reuse = false;
                if (labels.Contains(ac.TargetName))
                {
                    NamedReference @ref = new(ac.FirstToken, ac.TargetName);
                    @ref.ResolveToLabel();
                    FunctionCallByName fcn = new(ac.FirstToken, @ref);
                    if (emit)
                    {
                        analyzedStatements.Add(fcn);
                    }
                    return fcn;
                }
                else if (stringDirectives.ContainsKey(ac.TargetName))
                {
                    throw new AnalysisException(ac, $"string-mode macros must be called with 1 STRING argument");
                }
                else if (macroNames.ContainsKey(ac.TargetName))
                {
                    VerifyMacro(ac, ac.TargetName, [], emit); // TODO this won't hold
                }
                else
                {
                    throw new AnalysisException(ac, $"'{ac.TargetName}' does not name a known label or macro");
                }
            }
            else if (statement is DataDeclaration)
            {
                
            }

            if (reuse && emit)
            {
                analyzedStatements.Add(statement);
            }

            return statement;
        }

        private ControlFlowStatement AnalyzeControlFlowStatement(ControlFlowStatement statement, bool emit)
        {
            if (statement is IfStatement ifStatement)
            {
                AnalyzeCondition(ifStatement.Condition);
                if (ifStatement.Body != null)
                {
                    ifStatement.Body = AnalyzeUnknownStatement(ifStatement.Body, false);
                }
            }
            else if (statement is IfElseBlock ifElseBlock)
            {
                AnalyzeCondition(ifElseBlock.Condition);
                ifElseBlock.ThenBody = AnalyzeCollection(ifElseBlock.ThenBody, false);
                ifElseBlock.ElseBody = AnalyzeCollection(ifElseBlock.ElseBody, false);
            }
            else if (statement is LoopStatement loopStatement)
            {
                loopStatement.Body = AnalyzeCollection(loopStatement.Body, false);
            }
            else if (statement is WhileStatement whileStatement)
            {
                AnalyzeCondition(whileStatement.Condition);
                whileStatement.Statement = AnalyzeUnknownStatement(whileStatement.Statement, false);
            }
            else
            {
                throw new AnalysisException(statement, "control flow statement not recognized");
            }
            if (emit)
            {
                analyzedStatements.Add(statement);
            }
            return statement;
        }

        private void AnalyzeCondition(ConditionalExpression expression)
        {
            VerifyVRegister(expression.LeftHandOperand);

            expression.ResolveLeft(ResolveRValue(expression.LeftHandOperand, true));

            if (expression is KeystrokeCondition kc)
            {
                VerifyVRegister(kc.LeftHandOperand);
            }
            else if (expression is BinaryConditionalExpression bc)
            {
                bool leftIsReg = IsVRegister(bc.LeftHandOperand);
                bool rightIsReg = IsVRegister(bc.RightHandOperand);
                if (!leftIsReg && !rightIsReg)
                {
                    throw new AnalysisException(bc, "condition must include at least one register reference");
                }
                if (!leftIsReg)
                {
                    VerifyRValue(bc.LeftHandOperand, true, true);
                }
                if (!rightIsReg)
                {
                    VerifyRValue(bc.RightHandOperand, true, true);
                }
                bc.ResolveRight(ResolveRValue(bc.RightHandOperand, true));
            }
        }

        private void AnalyzeCalculation(CalculationExpression expression)
        {
            inCalculation = true;
            AnalyzeCalculationRecursive(expression);
            inCalculation = false;
        }

        private void AnalyzeCalculationRecursive(CalculationExpression expression)
        {
            if (expression is CalculationBinaryOperation cbo)
            {
                AnalyzeCalculationRecursive(cbo.Left);
                AnalyzeCalculationRecursive(cbo.Right);
            }
            else if (expression is CalculationUnaryOperation cuo)
            {
                if (cuo.Operator == CalculationUnaryOperation.UnaryOperator.AddressOf)
                {
                    if (cuo.Expression is CalculationName cn)
                    {
                        if (!labels.Contains(cn.Name.Name))
                        {
                            throw new AnalysisException(cn, "label expected to follow address-of operator '@'");
                        }
                    }
                    else
                    {
                        throw new AnalysisException(cuo, "cannot take the address of a non-name");
                    }
                }
                else
                {
                    AnalyzeCalculationRecursive(cuo.Expression);
                }
            }
            else if (expression is CalculationStringLengthOperation)
            {

            }
            else if (expression is CalculationName cn)
            {
                if (IsVRegister(cn.Name))
                {
                    throw new AnalysisException(cn, "calculations cannot reference registers");
                }

                cn.Resolve(ResolveNamedReference(cn.Name, true));
            }
            else if (expression is CalculationNumber)
            {

            }
            else
            {
                throw new AnalysisException(expression, "unknown calculation sub-type");
            }
        }

        private void VerifyNameUnique(AstNode node, string name, bool allowLabelDuplicates = false, bool allowStringModeDuplicates = false)
        {
            if (registerAliases.ContainsKey(name) || registerConstants.ContainsKey(name))
            {
                throw new AnalysisException(node, $"alias with name '{name}' already declared");
            }
            if (constants.ContainsKey(name))
            {
                throw new AnalysisException(node, $"constant with name '{name}' already declared");
            }
            if (!allowLabelDuplicates && labels.Contains(name))
            {
                throw new AnalysisException(node, $"label with name '{name}' already declared");
            }
            if (macroNames.ContainsKey(name))
            {
                throw new AnalysisException(node, $"macro with name '{name}' already declared");
            }
            if (calcs.ContainsKey(name))
            {
                throw new AnalysisException(node, $"calc with name '{name}' already declared");
            }
            if (!allowStringModeDuplicates && stringDirectives.ContainsKey(name))
            {
                throw new AnalysisException(node, $"stringmode with name '{name}' already declared");
            }
        }

        private void VerifyNameExistsInContext(AstNode node, string name, bool allowKeywords)
        {
            if (Lexer.TryParseNumber(name, out int number))
            {

            }
            else if (ResolveRegisterReference(new NamedReference(node.FirstToken, name)) != null)
            {

            }
            else if (IsReserved(name))
            {
                if (allowKeywords)
                {
                    VerifyConstant(node, name);
                }
                else
                {
                    throw new AnalysisException(node, $"keyword '{name}' is not allowed in this context");
                }
            }
            else if (TryGetMacroSubstitution(name, out string value))
            {
                if (Regex.IsMatch(Lexer.VRegisterLiteralRegex, value))
                {

                }
                else
                {
                    VerifyNameExistsInContext(node, value, allowKeywords);
                }
            }
            else if (!labels.Contains(name) &&
                !constants.ContainsKey(name) &&
                !calcs.ContainsKey(name))
            {
                throw new AnalysisException(node, $"'{name}' does not name a known label, constant, or calculation");
            }
        }

        private RValue ResolveRValue(RValue value, bool allowKeywords)
        {
            if (value is NumericLiteral)
            {
                return value;
            }
            else if (value is GenericRegisterReference reg)
            {
                ResolveRegisterReference(ref reg);
                return reg;
            }
            else if (value is NamedReference @ref)
            {
                GenericRegisterReference tmp = ResolveRegisterReference(@ref);
                if (tmp != null)
                {
                    return tmp;
                }
                return ResolveNamedReference(@ref, allowKeywords);
            }
            else
            {
                throw new AnalysisException(value, "unknown RValue sub-type");
            }
        }

        private GenericRegisterReference? ResolveRegisterReference(NamedReference @ref)
        {
            GenericRegisterReference reg = new(@ref.FirstToken, @ref);
            if (registerAliases.TryGetValue(@ref.Name, out byte index))
            {
                reg.Resolve(index);
            }
            else if (registerConstants.TryGetValue(@ref.Name, out CalculationExpression expr))
            {
                reg.Resolve(expr);
            }
            else if (Regex.IsMatch(@ref.Name, Lexer.VRegisterLiteralRegex))
            {
                reg.Resolve(Convert.ToByte(@ref.Name[1..], 16));
            }
            else if (TryGetMacroSubstitution(@ref.Name, out string value))
            {
                return ResolveRegisterReference(new NamedReference(@ref.FirstToken, value));
            }
            else
            {
                return null;
            }
            return reg;
        }

        private void ResolveRegisterReference(ref GenericRegisterReference reg)
        {
            if (!reg.Index.HasValue)
            {
                reg = ResolveRegisterReference(reg.Name) ??
                    throw new AnalysisException(reg, $"register name reference '{reg.Name}' could not be resolved");
            }
        }

        private RValue ResolveNamedReference(NamedReference @ref, bool allowKeywords)
        {
            VerifyNameExistsInContext(@ref, allowKeywords);

            if (Lexer.TryParseNumber(@ref.Name, out int number))
            {
                return new NumericLiteral(@ref.FirstToken, number);
            }
            else if (TryGetMacroSubstitution(@ref.Name, out string s))
            {
                NamedReference sub = new(@ref.FirstToken, s);
                return ResolveNamedReference(sub, allowKeywords);
            }
            else if (IsReserved(@ref.Name))
            {
                return new NumericLiteral(@ref.FirstToken, Lexer.Constants[@ref.Name]);
            }
            else if (labels.Contains(@ref.Name))
            {
                @ref.ResolveToLabel();
                NamedReference r = new(@ref.FirstToken, @ref.Name);
                r.ResolveToLabel();
                return r;
            }
            else if (calcs.TryGetValue(@ref.Name, out CalculationExpression expr))
            {
                @ref.ResolveToExpression(expr);
                return @ref;
            }
            else if (constants.TryGetValue(@ref.Name, out RValue value))
            {
                if (value is NumericLiteral num)
                {
                    return num;
                }
                else if (value is GenericRegisterReference reg)
                {
                    ResolveRegisterReference(ref reg);
                    return reg;
                }
                else if (value is NamedReference named)
                {
                    return ResolveNamedReference(named, allowKeywords);
                }
                else
                {
                    throw new AnalysisException(@ref, "unknown RValue sub-type");
                }
            }

            GenericRegisterReference? tmp = ResolveRegisterReference(@ref);
            if (tmp != null)
            {
                return tmp;
            }

            throw new AnalysisException(@ref, $"cannot resolve name '{@ref.Name}'");
        }

        private void VerifyNameExistsInContext(NamedReference name, bool allowKeywords)
        {
            VerifyNameExistsInContext(name, name.Name, allowKeywords);
        }

        private void VerifyNameExistsInContext(RValue value, bool allowKeywords)
        {
            if (value is NamedReference @ref)
            {
                VerifyNameExistsInContext(@ref, allowKeywords);
            }
        }

        private void VerifyVRegister(RValue value)
        {
            if (!IsVRegister(value))
            {
                throw new AnalysisException(value, $"{value} must refer to a \"v\" register");
            }
        }

        private bool IsVRegister(RValue value)
        {
            if (value is NumericLiteral)
            {
                return false;
            }
            string s;
            if (value is GenericRegisterReference reg)
            {
                if (reg.Index.HasValue)
                {
                    return true;
                }
                s = reg.Name.Name;
            }
            else if (value is NamedReference @ref)
            {
                s = @ref.Name;
            }
            else
            {
                throw new AnalysisException(value, "unknown RValue sub-type");
            }

            return !IsReserved(s) &&
                (registerAliases.ContainsKey(s) ||
                 registerConstants.ContainsKey(s) ||
                 (TryGetMacroSubstitution(s, out string name) && Regex.IsMatch(name, Lexer.VRegisterLiteralRegex)));
        }

        private static bool IsReserved(string name)
        {
            return Lexer.Constants.ContainsKey(name) || Lexer.CalcKeywords.Contains(name);
        }

        private bool IsConstantAllowed(string name)
        {
            if (name == "CALLS")
            {
                return currentMacro != null;
            }
            if (Regex.IsMatch(name, "^OCTO_KEY_[1234qwerasdfzxcv]$", RegexOptions.IgnoreCase))
            {
                return true;
            }
            if (Lexer.CalcKeywords.Contains(name))
            {
                return inCalculation;
            }
            if (name == "CHAR" || name == "INDEX" || name == "VALUE")
            {
                return currentStringMode != null;
            }
            throw new Exception("this function should only be called if `IsReserved` returns true");
        }

        private void VerifyRValue(RValue value, bool allowRegister, bool allowKeywords)
        {
            if (value is NumericLiteral)
            {

            }
            else if (IsVRegister(value))
            {
                if (!allowRegister)
                {
                    throw new AnalysisException(value, "register not allowed in this context");
                }
            }
            else if (value is NamedReference @ref)
            {
                VerifyNameExistsInContext(@ref, allowKeywords);
            }
            else
            {
                throw new AnalysisException(value, "unknown RValue sub-type");
            }
        }

        private void VerifyConstant(AstNode node, string name)
        {
            if (IsReserved(name))
            {
                if (!IsConstantAllowed(name))
                {
                    throw new AnalysisException(node, $"the constant `{name}` is not allowed in this context");
                }
            }
        }

        private void VerifyMacro(AstNode node, string macroName, string[] suppliedArgs, bool emit)
        {
            if (macroNames.TryGetValue(macroName, out MacroDefinition def))
            {
                if (suppliedArgs.Length != def.Arguments.Length)
                {
                    throw new AnalysisException(node, $"macro {macroName} expected {def.Arguments.Length} arguments, {suppliedArgs} supplied");
                }
                Dictionary<string, string> currentScope = [];
                macroCalls[def] += 1;
                currentScope["CALLS"] = macroCalls[def].ToString();
                for (int i = 0; i < def.Arguments.Length; i++)
                {
                    currentScope[def.Arguments[i]] = suppliedArgs[i];
                }
                macroSubstitutions.AddFirst(currentScope);
                currentMacro = macroName;

                AnalyzeCollection(def.Body, emit);

                currentMacro = null;
                macroSubstitutions.RemoveFirst();
            }
            else if (stringDirectives.TryGetValue(macroName, out Dictionary<string, StringDirective> byAlphabet))
            {
                if (suppliedArgs.Length != 1)
                {
                    throw new AnalysisException(node, $"string-mode macros must be called with one string argument");
                }
                string text = suppliedArgs[0];
                string? alphabet = byAlphabet.Keys.SingleOrDefault(k => text.All(c => k.Contains(c)));
                if (alphabet == null)
                {
                    throw new AnalysisException(node, $"no string-mode declared whose alphabet covers \"{text}\"");
                }
                StringDirective sd = byAlphabet[alphabet];

                for (int i = 0; i < text.Length; i++)
                {
                    Dictionary<string, string> currentScope = [];
                    currentScope["CHAR"] = ((int)text[i]).ToString();
                    currentScope["INDEX"] = i.ToString();
                    int value = sd.Alphabet.IndexOf(text[i]);
                    if (value < 0)
                    {
                        throw new AnalysisException(node, $"char '{text[i]}' is not in alphabet '{sd.Alphabet}'");
                    }
                    currentScope["VALUE"] = value.ToString();

                    macroSubstitutions.AddFirst(currentScope);
                    macroCalls[sd] += 1;
                    currentScope["CALLS"] = macroCalls[sd].ToString();
                    currentMacro = macroName;
                    currentStringMode = macroName;

                    AnalyzeCollection(sd.Body, emit);

                    currentMacro = null;
                    macroSubstitutions.RemoveFirst();
                }
            }
            else
            {
                throw new AnalysisException(node, $"{macroName} does not name a declared macro");
            }
        }

        public Statement[] AnalyzeCollection(Statement[] statements, bool emit = true)
        {
            List<Statement> localStatements = [];
            for (int i = 0; i < statements.Length; i++)
            {
                Statement analyzedStatement = AnalyzeUnknownStatement(statements[i].DeepCopy() as Statement, emit);
                localStatements.Add(analyzedStatement);
            }
            return localStatements.ToArray();
        }

        public bool TryGetMacroSubstitution(string param, out string value)
        {
            foreach (Dictionary<string, string> scope in macroSubstitutions)
            {
                if (scope.TryGetValue(param, out value))
                {
                    return true;
                }
            }
            value = string.Empty;
            return false;
        }

        private static void VerifyNumber(NumericLiteral num, byte numBits)
        {
            try
            {
                ushort word = Convert.ToUInt16(num.Value);
                if (word >= (ushort)Math.Pow(2, numBits))
                {
                    throw new AnalysisException(num, $"{num.Value} does not fit within {numBits} bits");
                }
            }
            catch
            {
                throw new AnalysisException(num, $"{num.Value} is not an integer");
            }
        }

        public class AnalysisException(string message) : Exception(message)
        {
            public AnalysisException(AstNode node, string message) :
                this($"line {node.FirstToken.Line}, '{node}': {message}")
            {

            }
        }
    }
}
