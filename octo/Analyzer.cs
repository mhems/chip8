using static octo.Lexer;
using System.Text.RegularExpressions;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;

namespace octo
{

    public class Analyzer(List<Statement> statements)
    {
        private readonly List<Statement> statements = statements;
        private int pos = 0;

        public Statement[] Analyze()
        {
            List<Statement> compiledStatements = [];



            return compiledStatements.ToArray();
        }

        public class AnalysisException(string message) : Exception(message)
        {
            public AnalysisException(Statement statement, string message) :
                this($"line {statement.FirstToken.Line}, '{statement}': {message}")
            {

            }
        }
    }
}
