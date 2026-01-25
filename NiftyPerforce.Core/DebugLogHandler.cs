// SPDX-FileCopyrightText: 2006-2017 Jim Tilander
// SPDX-FileCopyrightText: 2017-2026 Lambert Clara
//
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace NiftyPerforce.Core
{
    public class DebugLogHandler : Log.IHandler
    {
        public void OnMessage(Log.Level level, string message, string formattedLine)
        {
            Debug.Write(formattedLine);
        }
    }
}
