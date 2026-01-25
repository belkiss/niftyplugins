// SPDX-FileCopyrightText: 2006-2017 Jim Tilander
// SPDX-FileCopyrightText: 2017-2026 Lambert Clara
//
// SPDX-License-Identifier: MIT

using System;
using System.Diagnostics;

namespace NiftyPerforce.Core
{
    public enum P4Commands
    {
        /// <summary>
        /// p4 add.
        /// </summary>
        Add,

        /// <summary>
        /// p4 delete.
        /// </summary>
        Delete,

        /// <summary>
        /// p4 edit.
        /// </summary>
        Edit,

        /// <summary>
        /// p4 edit, but called without checking if the file is read-only or not.
        /// </summary>
        EditForce,

        /// <summary>
        /// p4 revert.
        /// </summary>
        Revert,

        /// <summary>
        /// p4 revert only if file is unchanged.
        /// </summary>
        RevertUnchanged,

        /// <summary>
        /// p4 revert and delete files that were opened for add.
        /// </summary>
        RevertDeleteOpenForAdd,

        /// <summary>
        /// p4 submit.
        /// </summary>
        Submit,
    }

    public class P4ExeWrapper
    {
        public string? P4ExecutablePath { get; private set; }

        private readonly P4Utils _p4Utils;
        private Version? _p4Version;

        public P4ExeWrapper(P4Utils p4Utils)
        {
            _p4Utils = p4Utils;
        }

        public bool Verify()
        {
            if (_p4Version == null || _p4Utils.GetFileVersion(P4ExecutablePath) == null)
            {
                P4ExecutablePath = _p4Utils.LocateP4InstallPath(P4Utils.P4ExeName);
                _p4Version = _p4Utils.GetFileVersion(P4ExecutablePath);
                if (_p4Version == null)
                {
                    Log.Error("P4 executable not found or version could not be determined at path: '{0}'", P4ExecutablePath ?? "<empty>");
                    return false;
                }
                else
                {
                    Log.Info("Found p4 at '{0}'", P4ExecutablePath!);
                }
            }

            return true;
        }

        public (string ExePath, string Arguments) GetArgumentsForCommand(P4Commands command, P4ConnectionInfo connectionInfo, string filePath, object[]? data = null)
        {
            string escapedFilePath = P4Utils.EscapeP4Path(filePath);
            Trace.Assert(connectionInfo.IsValid());
            switch (command)
            {
                case P4Commands.Add:
                    // filename doesn't need escaping when added, even if it contains special characters
                    return (P4ExecutablePath!, $"{connectionInfo.ConnectionString} add -f \"{filePath}\"");

                case P4Commands.Delete:
                    return (P4ExecutablePath!, $"{connectionInfo.ConnectionString} delete \"{escapedFilePath}\"");

                case P4Commands.Revert:
                case P4Commands.RevertUnchanged:
                case P4Commands.RevertDeleteOpenForAdd:
                    {
                        string revertArguments = string.Empty;
                        if (command == P4Commands.RevertUnchanged)
                            revertArguments = "-a ";
                        if (command == P4Commands.RevertDeleteOpenForAdd)
                            revertArguments = "-w ";
                        return (P4ExecutablePath!, $"{connectionInfo.ConnectionString} revert {revertArguments}\"{escapedFilePath}\"");
                    }

                case P4Commands.Submit:
                    string? description = data != null ? data[0] as string : string.Empty;
                    string extraArgument = !string.IsNullOrWhiteSpace(description) ? $"-d \"{description}\" " : string.Empty;
                    return (P4ExecutablePath!, $"{connectionInfo.ConnectionString} submit {extraArgument}\"{escapedFilePath}\"");

                case P4Commands.Edit:
                    throw new NotImplementedException();

                case P4Commands.EditForce:
                    throw new NotImplementedException();

                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }
    }
}
