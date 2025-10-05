// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;

namespace NiftyPerforce.Core
{
    public enum P4VCommands
    {
        /// <summary>
        /// Time lapse view.
        /// </summary>
        TimeLapseView,

        /// <summary>
        /// Revision graph.
        /// </summary>
        RevisionGraph,

        /// <summary>
        /// History.
        /// </summary>
        History,

        /// <summary>
        /// Browse to the current file.
        /// </summary>
        ShowFile,

        /// <summary>
        /// Diffs the current file.
        /// </summary>
        Diff,
    }

    /// <summary>
    /// Class that wraps P4VC functionality for a specified p4v executable.
    /// </summary>
    public class P4VExeWrapper
    {
        private const string P4VCBatContent = @"@echo off
start p4v.exe -p4vc %*
@rem wait a second to make sure write to files are flushed
timeout 2 > nul
if exist %TEMP%\p4vcout.out (
    type %TEMP%\p4vcout.out
)
if exist %TEMP%\p4vcerr.out (
    type %TEMP%\p4vcerr.out 1>&2
)
";

        private readonly IFileSystem _fileSystemService;
        private readonly P4Utils _p4Utils;

        private bool _p4vcBatFound;
        private string? _p4vcExecutablePath;
        private string? _p4vExecutablePath;

        private Version? _p4vVersion;

        public Version? GetVersion() => _p4vVersion;

        public P4VExeWrapper(IFileSystem fileSystemService, P4Utils p4Utils)
        {
            _fileSystemService = fileSystemService;
            _p4Utils = p4Utils;
        }

        public bool? ForceIgnore { get; set; }

        public bool Verify(P4VCommands command, string filePath)
        {
            if (_p4vVersion == null || _p4Utils.GetFileVersion(_p4vcExecutablePath) == null)
            {
                _p4vcExecutablePath = _p4Utils.LocateP4InstallPath(P4Utils.P4VcBatName);
                _p4vcBatFound = _p4vcExecutablePath != null && _fileSystemService.File.ReadAllText(_p4vcExecutablePath).Equals(P4VCBatContent, StringComparison.Ordinal);
                _p4vcExecutablePath ??= _p4Utils.LocateP4InstallPath(P4Utils.P4VcExeName);
                _p4vExecutablePath = _p4vcExecutablePath != null ? _fileSystemService.Path.Combine(_fileSystemService.Path.GetDirectoryName(_p4vcExecutablePath) !, P4Utils.P4VExeName) : null;
                _p4vVersion = _p4vExecutablePath != null ? _p4Utils.GetFileVersion(_p4vExecutablePath) : null;
                if (_p4vVersion == null)
                {
                    Log.Error("P4 executable not found or version could not be determined at path: '{0}'", _p4vcExecutablePath ?? "<empty>");
                    return false;
                }
                else
                {
                    Log.Info("Found p4v at '{0}'", _p4vcExecutablePath!);
                }
            }

            bool forceIgnore = ForceIgnore ?? false;

            switch (command)
            {
                case P4VCommands.TimeLapseView:
                    if (!forceIgnore && (filePath.Contains('#') || filePath.Contains('%') || filePath.Contains('@')))
                        return false;

                    break;

                case P4VCommands.RevisionGraph:
                    if (!forceIgnore && (filePath.Contains('#') || filePath.Contains('%') || filePath.Contains('@')))
                        return false;

                    break;

                case P4VCommands.ShowFile:
                    if (!forceIgnore && !WorkspaceWindowSupported(_p4vVersion) && filePath.Contains('@'))
                        return false;

                    break;

                case P4VCommands.History:
                    if (!forceIgnore && filePath.Contains(P4VCHistorySupported(_p4vVersion) ? '#' : '@'))
                        return false;

                    break;

                case P4VCommands.Diff:
                    if (!DiffHaveSupported(_p4vVersion))
                        return false;

                    break;

                default:
                    Log.Error("Unknown P4V command: {0}", command);
                    break;
            }

            return true;
        }

        private (string exe, string arguments) RemapToP4VIfNeeded(string exe, string arguments)
        {
            if (_p4vcBatFound)
            {
                return (_p4vExecutablePath!, $"-p4vc {arguments}");
            }

            return (exe, arguments);
        }

        public (string exe, string arguments) GetArgumentsForCommand(P4VCommands command, P4ConnectionInfo connectionInfo, string filePath)
        {
            string escapedFilePath = P4Utils.EscapeP4Path(filePath);
            Trace.Assert(connectionInfo.IsValid());
            switch (command)
            {
                case P4VCommands.TimeLapseView:
                    return RemapToP4VIfNeeded(_p4vcExecutablePath!, $"tlv \"{escapedFilePath}\"");
                case P4VCommands.RevisionGraph:
                    return RemapToP4VIfNeeded(_p4vcExecutablePath!, $"revisiongraph \"{escapedFilePath}\"");
                case P4VCommands.ShowFile: // this command specifically doesn't need escaped file paths for some reason...
                    if (WorkspaceWindowSupported(_p4vVersion))
                    {
                        return RemapToP4VIfNeeded(_p4vcExecutablePath!, $"workspacewindow -s \"{filePath}\"");
                    }

                    return (_p4vExecutablePath!, $"{connectionInfo.ConnectionString} -win 0 -s \"{filePath}\"");

                case P4VCommands.History:
                    if (P4VCHistorySupported(_p4vVersion))
                    {
                        return RemapToP4VIfNeeded(_p4vcExecutablePath!, $"history \"{filePath}\"");
                    }

                    return (_p4vExecutablePath!, $"{connectionInfo.ConnectionString} -win 0 -cmd \"history {filePath}\"");

                case P4VCommands.Diff:
                    return RemapToP4VIfNeeded(_p4vcExecutablePath!, $"diffhave \"{escapedFilePath}\"");

                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }

        public static bool WorkspaceWindowSupported(Version? version)
        {
            if (version == null)
                return false;

            // workspacewindow was added in p4v 2023.2/2443448, and 2024.1/2573667 deprecated p4v -s and p4v -t
            return version.Major > 2023 || (version.Major == 2023 && version.Minor >= 2);
        }

        public static bool DiffHaveSupported(Version? version)
        {
            if (version == null)
                return false;

            // diffhave was added in p4v 2020.1/1946989
            return version.Major >= 2020;
        }

        public static bool P4VCHistorySupported(Version? version)
        {
            if (version == null)
                return false;

            // history was added in p4v 2019.2 update1/1883366
            return version.Major > 2019 || (version.Major == 2019 && version.Minor >= 2);
        }
    }
}
