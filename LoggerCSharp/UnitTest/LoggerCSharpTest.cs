using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest
{
    [TestClass]
    public class LoggerCSharpTest
    {
        [TestMethod]
        public void WriteLog()
        {
            //before used,go to 'HKEY_LOCAL_MACHINE\SOFTWARE\LoggerCSharp' config 'testlog' to 'Trace' or 'Info' or 'Error'...
            LoggerCSharp.LogUtil.Logger.Setup("testlog");
            int a = 10;
            while (a-- > 0)
            {
                LoggerCSharp.LogUtil.Logger.LogInfo("你好");
            }
        }
    }
}