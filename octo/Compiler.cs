using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace octo
{
    public static class Compiler
    {
        public static void Compile(string srcPath)
        {
            List<Lexer.Token> tokens = Lexer.Lex(srcPath);
            Parser parser = new(tokens);
            Statement[] statements = parser.Parse();
            Analyzer analyzer = new(statements);
            statements = analyzer.Analyze();
            Generator generator = new(statements);
            byte[] program = generator.Generate();

            string dir = Path.GetDirectoryName(srcPath);
            string filename = Path.GetFileNameWithoutExtension(srcPath);
            string output_path = Path.Join(dir, "my_" + filename + ".ch8");
            File.WriteAllBytes(output_path, program);
        }
    }
}
