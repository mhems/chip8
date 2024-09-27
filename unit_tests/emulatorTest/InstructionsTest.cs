using emulator;
using System;
using System.Reflection.Emit;
using System.Reflection.Metadata;

namespace emulatorTest
{
    [TestClass]
    public class InstructionsTest
    {
        [DataRow(0x00E0, "CLR", new ushort[0], "CLR")]
        [DataRow(0x00EE, "RET", new ushort[0], "RET")]
        [DataRow(0x1000, "JMPI", new ushort[1] { 0x0 }, "JMPI 0x0")]
        [DataRow(0x100A, "JMPI", new ushort[1] { 0xA }, "JMPI 0xA")]
        [DataRow(0x10AB, "JMPI", new ushort[1] { 0xAB }, "JMPI 0xAB")]
        [DataRow(0x1ABC, "JMPI", new ushort[1] { 0xABC }, "JMPI 0xABC")]
        [DataRow(0x2000, "CAL", new ushort[1] { 0x0 }, "CAL 0x0")]
        [DataRow(0x200A, "CAL", new ushort[1] { 0xA }, "CAL 0xA")]
        [DataRow(0x20AB, "CAL", new ushort[1] { 0xAB }, "CAL 0xAB")]
        [DataRow(0x2ABC, "CAL", new ushort[1] { 0xABC }, "CAL 0xABC")]
        [DataRow(0x3000, "SEQI", new ushort[2] { 0, 0 }, "SEQI 0x0 0x0")]
        [DataRow(0x390B, "SEQI", new ushort[2] { 0x9, 0xB }, "SEQI 0x9 0xB")]
        [DataRow(0x3FFF, "SEQI", new ushort[2] { 0xF, 0xFF }, "SEQI 0xF 0xFF")]
        [DataRow(0x4000, "SNEI", new ushort[2] { 0, 0 }, "SNEI 0x0 0x0")]
        [DataRow(0x490B, "SNEI", new ushort[2] { 0x9, 0xB }, "SNEI 0x9 0xB")]
        [DataRow(0x4FFF, "SNEI", new ushort[2] { 0xF, 0xFF }, "SNEI 0xF 0xFF")]
        [DataRow(0x5000, "SEQ", new ushort[2] { 0, 0 }, "SEQ 0x0 0x0")]
        [DataRow(0x5450, "SEQ", new ushort[2] { 4, 5 }, "SEQ 0x4 0x5")]
        [DataRow(0x5090, "SEQ", new ushort[2] { 0, 9 }, "SEQ 0x0 0x9")]
        [DataRow(0x6000, "SETI", new ushort[2] { 0, 0 }, "SETI 0x0 0x0")]
        [DataRow(0x690B, "SETI", new ushort[2] { 0x9, 0xB }, "SETI 0x9 0xB")]
        [DataRow(0x6FFF, "SETI", new ushort[2] { 0xF, 0xFF }, "SETI 0xF 0xFF")]
        [DataRow(0x7000, "ADDI", new ushort[2] { 0, 0 }, "ADDI 0x0 0x0")]
        [DataRow(0x790B, "ADDI", new ushort[2] { 0x9, 0xB }, "ADDI 0x9 0xB")]
        [DataRow(0x7FFF, "ADDI", new ushort[2] { 0xF, 0xFF }, "ADDI 0xF 0xFF")]
        [DataRow(0xD000, "DRAW", new ushort[3] { 0, 0, 0 }, "DRAW 0x0 0x0 0x0")]
        [DataRow(0xDABC, "DRAW", new ushort[3] { 0xA, 0xB, 0xC }, "DRAW 0xA 0xB 0xC")]
        [DataRow(0xD103, "DRAW", new ushort[3] { 1, 0, 3 }, "DRAW 0x1 0x0 0x3")]
        [DataRow(0xE09E, "SKEQ", new ushort[1] { 0 }, "SKEQ 0x0")]
        [DataRow(0xE79E, "SKEQ", new ushort[1] { 7 }, "SKEQ 0x7")]
        [TestMethod]
        public void TestNumberToInstruction(int num,
            string mnemonic,
            ushort[] args,
            string code)
        {
            string opcode = Instructions.InstructionFormatMap.Single(kv => kv.Value.Mnemonic == mnemonic).Key;

            Instruction instruction1 = new((ushort)num);
            TestInstruction(instruction1, num, code, mnemonic, opcode, args);

            Instruction instruction2 = new(mnemonic, args);
            TestInstruction(instruction2, num, code, mnemonic, opcode, args);
        }

        private static void TestInstruction(Instruction observedInstruction,
            int expectedValue,
            string expectedCode,
            string expectedMnemonic,
            string expectedOpcode,
            ushort[] expectedArguments)
        {
            Assert.AreEqual(expectedValue, observedInstruction.Value);
            Assert.AreEqual(expectedCode, observedInstruction.Code);
            Assert.AreEqual(expectedMnemonic, observedInstruction.Mnemonic);
            Assert.AreEqual(expectedOpcode, observedInstruction.OpCode);
            CollectionAssert.AreEqual(expectedArguments, observedInstruction.Arguments);
        }
    }
}