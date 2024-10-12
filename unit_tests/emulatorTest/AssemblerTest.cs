using emulator;
using System;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Security.Cryptography;

namespace emulatorTest
{
    [TestClass]
    public class AssemblerTest
    {
        private const string outputName = "output.ch8";

        [DataRow("simple.8o", "simple.ch8")]
        [TestMethod]
        public void TestAssembler(string srcPath, string expectedPath)
        {
            Assembler asm = new();
            asm.Assemble(srcPath, outputName);
            byte[] observedBytes = File.ReadAllBytes(outputName);
            byte[] expectedBytes = File.ReadAllBytes(expectedPath);
            CollectionAssert.AreEqual(expectedBytes, observedBytes);
        }
    }
}