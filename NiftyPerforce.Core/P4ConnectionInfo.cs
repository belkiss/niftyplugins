// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.

using System.Collections.Generic;

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
    }
}
