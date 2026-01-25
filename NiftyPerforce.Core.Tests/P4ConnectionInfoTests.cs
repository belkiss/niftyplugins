// SPDX-FileCopyrightText: 2006-2017 Jim Tilander
// SPDX-FileCopyrightText: 2017-2026 Lambert Clara
//
// SPDX-License-Identifier: MIT

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NiftyPerforce.Core.Tests
{
    [TestClass]
    public class P4ConnectionInfoTests
    {
        [TestMethod]
        public void ParseP4Set()
        {
            const string P4SetOutput = @"
P4CHARSET=utf8
P4CLIENT=some.user_ClientName
P4CONFIG=p4config.txt
P4EDITOR=C:\Users\user\AppData\Local\Programs\Microsoft VS Code\Code.exe -n -w
P4IGNORE=.p4ignore.txt
P4PORT=ssl:someport:1666
P4USER=some.user
exit: 0
";
            var connectionInfo = P4ConnectionInfo.FromP4SetOutput(P4SetOutput);
            Assert.IsNotNull(connectionInfo);
            Assert.AreEqual("some.user_ClientName", connectionInfo.Client);
            Assert.AreEqual("ssl:someport:1666", connectionInfo.Server);
            Assert.AreEqual("some.user", connectionInfo.Username);
            Assert.IsTrue(connectionInfo.IsValid());
            Assert.AreEqual("-p ssl:someport:1666 -u some.user -c some.user_ClientName", connectionInfo.ConnectionString);

            Assert.IsNull(P4ConnectionInfo.FromP4SetOutput(null));
            Assert.IsNull(P4ConnectionInfo.FromP4SetOutput(string.Empty));
            Assert.IsNull(P4ConnectionInfo.FromP4SetOutput("\n"));
        }

        [TestMethod]
        public void ParseP4Info()
        {
            const string P4InfoOutput = @"
User name: some.user
Client name: some.user_ClientName
Client host: HOST-NAME
Client root: C:\Users\username\testwks
Client stream: //Depot/Dev
Current directory: c:\Users\username\testwks
Peer address: 127.0.0.1:39668
Client address: 3.4.5.6
Server address: localhost:1667
Server root: /p4/1/root
Server date: 2025/10/05 15:36:13 +0000 UTC
Server uptime: 799:39:18
Server version: P4D/LINUX26X86_64/2025.1/2761706 (2025/05/09)
Server encryption: encrypted
Server cert expires: Mar 13 18:18:55 2034 GMT
ServerID: some_id
Server services: edge-server
Replica of: ssl:p4commit:1667
Changelist server: ssl:p4commit:1667
Broker address: someport:1666
Broker encryption: encrypted
Broker cert expires: Mar 13 18:18:55 2034 GMT
Broker version: P4BROKER/LINUX26X86_64/2025.1/2761706
Proxy address: 1.2.3.4:1666
Proxy cacheRoot: /p4/1/cache
Proxy root: /p4/1/cache
Proxy version: P4P/LINUX26X86_64/2023.2/2605454 (2024/05/31)
Proxy encryption: encrypted
Proxy cert expires: Feb 17 17:09:26 2034 GMT
Server license: none
Case Handling: sensitive";

            var connectionInfo = P4ConnectionInfo.FromP4InfoOutput(P4InfoOutput);
            Assert.IsNotNull(connectionInfo);
            Assert.AreEqual("some.user_ClientName", connectionInfo.Client);
            Assert.AreEqual("ssl:someport:1666", connectionInfo.Server);
            Assert.AreEqual("some.user", connectionInfo.Username);
            Assert.IsTrue(connectionInfo.IsValid());
            Assert.AreEqual("-p ssl:someport:1666 -u some.user -c some.user_ClientName", connectionInfo.ConnectionString);

            Assert.IsNull(P4ConnectionInfo.FromP4InfoOutput(null));
            Assert.IsNull(P4ConnectionInfo.FromP4InfoOutput(string.Empty));
            Assert.IsNull(P4ConnectionInfo.FromP4InfoOutput("\n"));
        }
    }
}
