// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.

using System.Diagnostics;

namespace Aurora
{
    public class DebugLogHandler : Log.IHandler
    {
        public void OnMessage(Log.Level level, string message, string formattedLine)
        {
            Debug.Write(formattedLine);
        }
    }
}
