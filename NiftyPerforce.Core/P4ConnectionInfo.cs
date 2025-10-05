// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NiftyPerforce.Core
{
    public class P4ConnectionInfo
    {
        public string? Client { get; set; }

        public string? Server { get; set; }

        public string? Username { get; set; }

        public bool IsValid() => !string.IsNullOrEmpty(Server) && !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Client);

        public string? ConnectionString => $"-p {Server} -u {Username} -c {Client}";

        public Dictionary<string, string> GetEnv() => new Dictionary<string, string>()
        {
            { "P4CLIENT", Client ?? string.Empty },
            { "P4PORT", Server ?? string.Empty },
            { "P4USER", Username ?? string.Empty },
        };

        public static P4ConnectionInfo? FromP4InfoOutput(string? p4InfoOutput)
        {
            if (string.IsNullOrWhiteSpace(p4InfoOutput))
                return null;

            var userpattern = new Regex(@"User name: (?<user>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
            var portpattern = new Regex(@"Server address: (?<port>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
            var brokerpattern = new Regex(@"Broker address: (?<port>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
            var proxypattern = new Regex(@"Proxy address: (?<port>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
            var clientpattern = new Regex(@"Client name: (?<client>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);

            Match usermatch = userpattern.Match(p4InfoOutput);
            Match portmatch = portpattern.Match(p4InfoOutput);
            Match brokermatch = brokerpattern.Match(p4InfoOutput);
            Match proxymatch = proxypattern.Match(p4InfoOutput);
            Match clientmatch = clientpattern.Match(p4InfoOutput);

            string port = portmatch.Groups["port"].Value.Trim();
            string? broker = brokermatch.Success ? brokermatch.Groups["port"].Value.Trim() : null;
            string? proxy = proxymatch.Success ? proxymatch.Groups["port"].Value.Trim() : null;
            string username = usermatch.Groups["user"].Value.Trim();
            string client = clientmatch.Groups["client"].Value.Trim();

            string server;
            Regex encryptionpattern;
            if (!string.IsNullOrEmpty(broker))
            {
                server = broker!;
                encryptionpattern = new Regex(@"Broker encryption: (?<encrypted>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
            }
            else if (!string.IsNullOrEmpty(proxy))
            {
                server = proxy!;
                encryptionpattern = new Regex(@"Proxy encryption: (?<encrypted>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
            }
            else
            {
                server = port;
                encryptionpattern = new Regex(@"Server encryption: (?<encrypted>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
            }

            Match encryptionmatch = encryptionpattern.Match(p4InfoOutput);
            bool encrypted = encryptionmatch.Success && encryptionmatch.Groups["encrypted"].Value.Trim() == "encrypted";
            if (encrypted)
            {
                server = $"ssl:{server}";
            }

            var connectionInfo = new P4ConnectionInfo
            {
                Client = client,
                Server = server,
                Username = username,
            };

            if (connectionInfo.IsValid())
            {
                Log.Debug("GetUserInfoStringFull : " + connectionInfo.ConnectionString);
                return connectionInfo;
            }

            return null;
        }

        public static P4ConnectionInfo? FromP4SetOutput(string? p4SetOutput)
        {
            if (string.IsNullOrWhiteSpace(p4SetOutput))
                return null;

            string? client = null;
            string? server = null;
            string? username = null;

            foreach (string s in p4SetOutput!.Split('\n'))
            {
                string trim = s.Trim();
                if (trim.StartsWith("P4CLIENT=", StringComparison.Ordinal))
                {
                    client = trim.Substring(9);
                }
                else if (trim.StartsWith("P4PORT=", StringComparison.Ordinal))
                {
                    server = trim.Substring(7);
                }
                else if (trim.StartsWith("P4USER=", StringComparison.Ordinal))
                {
                    username = trim.Substring(7);
                }
            }

            if (!string.IsNullOrEmpty(client) && !string.IsNullOrEmpty(server) && !string.IsNullOrEmpty(username))
            {
                return new P4ConnectionInfo
                {
                    Client = client,
                    Server = server,
                    Username = username,
                };
            }

            return null;
        }
    }
}
