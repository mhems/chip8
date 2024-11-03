using emulator;
using octo;
using System;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Security.Cryptography;

namespace emulatorTest
{
    [TestClass]
    public class OctoGeneratorTest
    {
        [DataRow("1-chip8-logo.8o")]
        [DataRow("2-ibm-logo.8o")]
        [DataRow("3-corax+.8o")]
        [DataRow("4-flags.8o")]
        [DataRow("5-quirks.8o")]
        [DataRow("6-keypad.8o")]
        [DataRow("7-beep.8o")]
        [TestMethod]
        public void TestGenerator(string srcPath)
        {
            List<Lexer.Token> tokens = Lexer.Lex(srcPath);
            Parser parser = new(tokens);
            Statement[] statements = parser.Parse();
            Analyzer analyzer = new(statements);
            statements = analyzer.Analyze();
            Generator generator = new(statements);
            generator.Generate();
        }
    }
}