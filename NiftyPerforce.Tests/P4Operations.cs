// Copyright (C) 2006-2017 Jim Tilander, 2017-2024 Lambert Clara. See the COPYING file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NiftyPerforce.Tests
{
    [TestClass]
    public class P4Operations
    {
        [TestMethod]
        public void ParseP4Set()
        {
            string p4SetOutput = @"
P4CHARSET=utf8
P4CLIENT=some.user_ClientName
P4CONFIG=p4config.txt
P4EDITOR=C:\Users\user\AppData\Local\Programs\Microsoft VS Code\Code.exe -n -w
P4IGNORE=.p4ignore.txt
P4PORT=ssl:someport:1666
P4USER=some.user
exit: 0
";
            Assert.AreEqual(
                "-p ssl:someport:1666 -u some.user -c some.user_ClientName",
                NiftyPerforce.P4Operations.GetConnectionStringFromP4SetOutput(p4SetOutput));
        }
    }
}
