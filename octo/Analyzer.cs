using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace octo
{
    public class Analyzer(Statement[] statements)
    {
        private readonly Statement[] statements = statements;
        private int posIn = 0;
        private ushort address = 0x200;
        private readonly Dictionary<string, byte> registerAliases = [];
        private readonly Dictionary<string, CalculationExpression> registerConstants = [];
        private readonly Dictionary<string, RValue> constants = [];
        private readonly HashSet<string> labels = [];
        private readonly HashSet<string> forwardDeclarations = [];
        private readonly Dictionary<string, CalculationExpression> calcs = [];
        private readonly Dictionary<string, MacroDefinition> macroNames = [];
        private readonly Dictionary<string, uint> macroCalls = [];
        private readonly LinkedList<Dictionary<string, string>> macroSubstitutions = [];
        private readonly Dictionary<string, StringDirective> stringDirectives = [];
        private readonly HashSet<string> breakpoints = [];
        private string? currentMacro = null;
        private string? currentStringMode = null;
        private bool inCalculation = false;

        public Statement[] Analyze()
        {
            List<Statement> compiledStatements = [];

            // first pass aggregates names and semantics
            while (posIn < statements.Length)
            {
                Statement statement = statements[posIn];
                AnalyzeUnknownStatement(statement);
                posIn++;
            }

            if (!labels.Contains("main"))
            {
                throw new AnalysisException(statements[0], "program must contain a 'main' label");
            }

            foreach (string decl in forwardDeclarations)
            {
                if (!labels.Contains(decl))
                {
                    throw new AnalysisException($"a label for the forward-declared '{decl}' was never found");
                }
            }

            // second pass expands macros and control flow; evaluates calculations

            return compiledStatements.ToArray();
        }

        private void AnalyzeUnknownStatement(Statement statement)
        {
            if (statement is Directive d)
            {
                AnalyzeDirective(d);
            }
            else if (statement is Assignment a)
            {
                AnalyzeAssignment(a);
            }
            else if (statement is ControlFlowStatement c)
            {
                AnalyzeControlFlowStatement(c);
            }
            else
            {
                AnalyzeStatement(statement);
            }
        }
 
        private void AnalyzeDirective(Directive directive)
        {
            if (directive is BreakpointDirective dd)
            {
                if (breakpoints.Contains(dd.Name))
                {
                    throw new AnalysisException(dd, $"breakpoint with name '{dd.Name}' already declared");
                }
                breakpoints.Add(dd.Name);
            }
            else if (directive is MonitorDirective)
            {

            }
            else if (directive is RegisterAlias ra)
            {
                VerifyNameUnique(ra, ra.Name);
                registerAliases[ra.Name] = ra.RegisterIndex;
            }
            else if (directive is ConstantAlias ca)
            {
                VerifyNameUnique(ca, ca.Name);
                // TODO assert 8 bits
                registerConstants[ca.Name] = ca.Expression;
            }
            else if (directive is LabelDeclaration ld)
            {
                VerifyNameUnique(ld, ld.Name);
                labels.Add(ld.Name);
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
            }
            else if (directive is UnpackDirective ud)
            {
                VerifyNameExistsInContext(ud.Name, true);

                if (ud is UnpackNumberDirective und)
                {
                    VerifyRValue(und.Value, false, true);
                    // TODO value is 4 bits
                }
            }
            else if (directive is NextDirective nd)
            {
                VerifyNameUnique(nd, nd.Label);
                labels.Add(nd.Label);
                AnalyzeUnknownStatement(nd.Statement);
            }
            else if (directive is OrgDirective od)
            {
                AnalyzeCalculation(od.Expression);
            }
            else if (directive is MacroDefinition md)
            {
                VerifyNameUnique(md, md.Name);
                macroNames[md.Name] = md;
                macroCalls[md.Name] = 0;
            }
            else if (directive is Calculation calc)
            {
                VerifyNameUnique(calc, calc.Name);

                AnalyzeCalculation(calc.Expression);

                // add entry after analysis so self-reference is not allowed
                calcs[calc.Name] = calc.Expression;
            }
            else if (directive is ValueByteDirective vbd)
            {
                VerifyRValue(vbd.Value, false, true);
            }
            else if (directive is ExpressionByteDirective ebd)
            {
                AnalyzeCalculation(ebd.Expression);
            }
            else if (directive is NamedPointerDirective npd)
            {
                try
                {
                    VerifyNameExistsInContext(npd.Name, true);
                }
                catch
                {
                    forwardDeclarations.Add(npd.Name.Name);
                }
            }
            else if (directive is PointerExpressionDirective ped)
            {
                AnalyzeCalculation(ped.Expression);
            }
            else if (directive is StringDirective sd)
            {
                VerifyNameUnique(sd, sd.Name);
                currentStringMode = sd.Name;
                foreach (Statement statement in sd.Body)
                {
                    AnalyzeUnknownStatement(statement);
                }
                currentStringMode = null;

                stringDirectives[sd.Name] = sd;
            }
            else if (directive is AssertDirective ad)
            {
                AnalyzeCalculation(ad.Expression);
            }
            else
            {
                throw new AnalysisException(directive, "unknown directive sub-type");
            }
        }

        private void AnalyzeAssignment(Assignment assignment)
        {
            if (assignment is LoadDelayAssignment lda)
            {
                VerifyVRegister(lda.SourceRegister);
            }
            else if (assignment is SaveDelayAssignment sda)
            {
                VerifyVRegister(sda.DestinationRegister);
            }
            else if (assignment is LoadBuzzerAssignment lba)
            {
                VerifyVRegister(lba.SourceRegister);
            }
            else if (assignment is MemoryAssignment ma)
            {
                if (ma.Address != null)
                {
                    VerifyNumber(ma.Address, 12);
                }
                else
                {
                    VerifyNameExistsInContext(ma.Name, true);
                }
            }
            else if (assignment is MemoryIncrementAssignment mia)
            {
                VerifyVRegister(mia.Register);
            }
            else if (assignment is LoadCharacterAssignment lca)
            {
                VerifyVRegister(lca.Register);
            }
            else if (assignment is KeyInputAssignment kia)
            {
                VerifyVRegister(kia.DestinationRegister);
            }
            else if (assignment is RegisterAssignment ra)
            {
                VerifyVRegister(ra.DestinationRegister);
                VerifyVRegister(ra.SourceRegister);
            }
            else if (assignment is ImmediateAssignment ia)
            {
                VerifyVRegister(ia.DestinationRegister);
                VerifyRValue(ia.Value, false, true);
                // TODO verify 8 bits
            }
            else if (assignment is RandomAssignment rna)
            {
                VerifyVRegister(rna.DestinationRegister);
                VerifyRValue(rna.Mask, false, true);
                // TODO verify 8 bits
            }
            else
            {
                throw new AnalysisException(assignment, "unknown assignment sub-type");
            }
        }

        private void AnalyzeStatement(Statement statement)
        {
            if (statement is ReturnStatement)
            {

            }
            else if (statement is ClearStatement)
            {

            }
            else if (statement is BcdStatement bcds)
            {
                VerifyVRegister(bcds.Register);
            }
            else if (statement is LoadStatement ls)
            {
                VerifyVRegister(ls.Register);
            }
            else if (statement is SaveStatement ss)
            {
                VerifyVRegister(ss.Register);
            }
            else if (statement is SpriteStatement sps)
            {
                VerifyVRegister(sps.XRegister);
                VerifyVRegister(sps.YRegister);
                VerifyRValue(sps.Height, false, true);
                // TODO verify num is 4 bits
            }
            else if (statement is JumpStatement js)
            {
                if (js.TargetName != null)
                {
                    VerifyRValue(js.TargetName, false, false);
                }
                else
                {
                    // TODO verify num is 12 bits
                }
            }
            else if (statement is MacroInvocation mi)
            {
                VerifyMacro(mi, mi.MacroName, mi.Arguments);
            }
            else if (statement is AmbiguousCall ac)
            {
                if (!labels.Contains(ac.TargetName))
                {
                    if (macroNames.ContainsKey(ac.TargetName))
                    {
                        VerifyMacro(ac, ac.TargetName, []);
                    }
                    else
                    {
                        throw new AnalysisException(ac, $"'{ac.TargetName}' does not name a known label or macro");
                    }
                }
            }
            else if (statement is DataDeclaration)
            {
                // TODO verify num is 8 bits
            }
        }

        private void AnalyzeControlFlowStatement(ControlFlowStatement statement)
        {
            if (statement is IfStatement ifStatement)
            {
                AnalyzeCondition(ifStatement.Condition);
                if (ifStatement.Body != null)
                {
                    AnalyzeUnknownStatement(ifStatement.Body);
                }
            }
            else if (statement is IfElseBlock ifElseBlock)
            {
                AnalyzeCondition(ifElseBlock.Condition);
                foreach (Statement s in ifElseBlock.ThenBody)
                {
                    AnalyzeUnknownStatement(s);
                }
                foreach (Statement s in ifElseBlock.ElseBody)
                {
                    AnalyzeUnknownStatement(s);
                }
            }
            else if (statement is LoopStatement loopStatement)
            {
                foreach (Statement s in loopStatement.Body)
                {
                    AnalyzeUnknownStatement(s);
                }
            }
            else if (statement is WhileStatement whileStatement)
            {
                AnalyzeCondition(whileStatement.Condition);
                AnalyzeUnknownStatement(whileStatement.Statement);
            }
            else
            {
                throw new AnalysisException(statement, "control flow statement not recognized");
            }
        }

        private void AnalyzeCondition(ConditionalExpression expression)
        {
            if (expression is KeystrokeCondition kc)
            {
                VerifyVRegister(kc.LeftRegister);
            }
            else if (expression is BinaryConditionalExpression bc)
            {
                bool leftIsReg = IsVRegister(bc.LeftRegister);
                bool rightIsReg = IsVRegister(bc.RightHandOperand);
                if (!leftIsReg && !rightIsReg)
                {
                    throw new AnalysisException(bc, "condition must include at least one register reference");
                }
                if (!leftIsReg)
                {
                    VerifyRValue(bc.LeftRegister, true, true);
                }
                if (!rightIsReg)
                {
                    VerifyRValue(bc.RightHandOperand, true, true);
                }
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
                        if (!labels.Contains(cn.Name))
                        {
                            throw new AnalysisException(cn, "calculations cannot reference labels");
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
                if (registerAliases.ContainsKey(cn.Name) ||
                    registerConstants.ContainsKey(cn.Name))
                {
                    throw new AnalysisException(cn, "calculations cannot reference registers");
                }

                VerifyNameExistsInContext(cn, cn.Name, true);
            }
            else if (expression is CalculationNumber)
            {

            }
            else
            {
                throw new AnalysisException(expression, "unknown calculation sub-type");
            }
        }

        private void VerifyNameUnique(AstNode node, string name)
        {
            if (registerAliases.ContainsKey(name) || registerConstants.ContainsKey(name))
            {
                throw new AnalysisException(node, $"alias with name '{name}' already declared");
            }
            if (constants.ContainsKey(name))
            {
                throw new AnalysisException(node, $"constant with name '{name}' already declared");
            }
            if (labels.Contains(name))
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
            if (stringDirectives.ContainsKey(name))
            {
                throw new AnalysisException(node, $"stringmode with name '{name}' already declared");
            }
        }

        private void VerifyNameExistsInContext(AstNode node, string name, bool allowKeywords)
        {
            if (IsReserved(name))
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
            else if (!labels.Contains(name) &&
                !forwardDeclarations.Contains(name) &&
                !constants.ContainsKey(name) &&
                !calcs.ContainsKey(name))
            {
                throw new AnalysisException(node, $"'{name}' does not name a known label, constant, or calculation");
            }
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
                s = reg.Name;
            }
            else if (value is NamedReference @ref)
            {
                s = @ref.Name;
            }
            else
            {
                throw new AnalysisException(value, "unknown RValue sub-type");
            }

            return !IsReserved(s) && (registerAliases.ContainsKey(s) || registerConstants.ContainsKey(s));
        }

        private static bool IsReserved(string name)
        {
            return Lexer.Constants.Contains(name) || Lexer.CalcKeywords.Contains(name);
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
            if (IsVRegister(value))
            {
                if (!allowRegister)
                {
                    throw new AnalysisException(value, "register not allowed in this context");
                }
            }
            else
            {
                if (value is NamedReference @ref)
                {
                    VerifyNameExistsInContext(@ref, allowKeywords);
                }
                else
                {
                    throw new AnalysisException(value, "unknown RValue sub-type");
                }
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

        private void VerifyMacro(AstNode node, string macroName, string[] suppliedArgs)
        {
            if (macroNames.TryGetValue(macroName, out MacroDefinition def))
            {
                if (suppliedArgs.Length != def.Arguments.Length)
                {
                    throw new AnalysisException(node, $"macro {macroName} expected {def.Arguments.Length} arguments, {suppliedArgs} supplied");
                }
                Dictionary<string, string> currentScope = [];
                for (int i = 0; i < def.Arguments.Length; i++)
                {
                    currentScope[def.Arguments[i]] = suppliedArgs[i];
                }
                macroSubstitutions.AddFirst(currentScope);
                macroCalls[macroName] += 1;
                currentMacro = macroName;
                // TODO expand and add statements, verifying each statement
                // expansion includes substituting names (including calc exprs)
                currentMacro = null;
                macroSubstitutions.RemoveFirst();
            }
            else
            {
                throw new AnalysisException(node, $"{macroName} does not name a declared macro");
            }
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
