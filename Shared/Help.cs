// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.
using System;
using System.IO;

namespace Aurora
{
    public static class Help
    {
        private static readonly char[] s_semicolon = new[] { ';' };

        public static string? FindFileInPath(string filename)
        {
            string? pathenv = Environment.GetEnvironmentVariable("PATH");
            string[] items = pathenv?.Split(s_semicolon, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            foreach (string item in items)
            {
                string candidate = Path.Combine(item, filename);
                if (System.IO.File.Exists(candidate))
                    return candidate;
            }

            return null;
        }
    }
}
