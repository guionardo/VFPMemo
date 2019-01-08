using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFPMemo;

namespace Testes
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void MarshalSize()
        {
            Console.WriteLine(MEMVarInfo.HeaderSize);
        }

        [TestMethod]
        public void TestFile()
        {
            string arq = Path.GetFullPath("..\\..\\..\\test.mem");
            Console.WriteLine(arq);
            MEMVarInfo m = new MEMVarInfo();
            using (var f = new FileStream(arq, FileMode.Open))
            {
                while (m.ReadFromStream(f))
                {
                    Console.WriteLine(m.name + " = " + m.value.ToString());
                }
            }

        }
    }
}
