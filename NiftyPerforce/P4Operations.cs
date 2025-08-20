// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NiftyPerforce.Core;

namespace NiftyPerforce
{
    // Simplification wrapper around running perforce commands.
    public class P4Operations
    {
        private static readonly object s_opsInFlightLock = new object();
        private static readonly HashSet<string> s_opsInFlight = new HashSet<string>();

        internal static bool HasOpsInFlight
        {
            get
            {
                lock (s_opsInFlightLock)
                    return s_opsInFlight.Count > 0;
            }
        }

        private static readonly HashSet<string> s_alreadyNotified = new HashSet<string>();

        private bool _p4CustomDiff;
        private string? _p4FullPath;
        private string? _p4VFullPath;
        private string? _p4VcFullPath;

        private bool _p4VcHistorySupported;
        private bool _p4VcDiffHaveSupported;
        private bool _p4VcWorkspaceWindowSupported;

        private bool _ignoreReadOnlyOnEdit;
        private bool _useSystemEnv = true;
        private SettingsLookupSource _preferredLookupSource = SettingsLookupSource.P4Set;

        private string? _port;
        private string? _client;
        private string? _username;

        public void SetOptions(bool ignoreReadOnlyOnEdit, bool useSystemEnv, SettingsLookupSource preferredLookupSource, string port, string client, string username)
        {
            _ignoreReadOnlyOnEdit = ignoreReadOnlyOnEdit;
            _useSystemEnv = useSystemEnv;
            _preferredLookupSource = preferredLookupSource;
            _port = port;
            _client = client;
            _username = username;
        }

        private static bool LockOp(string token)
        {
            bool added;
            lock (s_opsInFlightLock)
            {
                added = s_opsInFlight.Add(token);
            }

            if (added)
            {
                Log.Debug("## Locked \"" + token + "\"");
                return true;
            }

            Log.Error(token + " already in progress");
            return false;
        }

        private static void UnlockOp(bool ok, object? tokenObject)
        {
            string? token = tokenObject as string;
            Trace.Assert(token != null, $"{nameof(UnlockOp)} must be called with a string token");

            bool removed;
            lock (s_opsInFlightLock)
            {
                removed = s_opsInFlight.Remove(token!);
            }

            if (removed)
            {
                Log.Debug("## Unlocked \"" + token + "\"");
            }
            else
            {
                Log.Debug("!! Failed to unlock \"" + token + "\"");
            }
        }

        private static string FormatToken(string operation, string filename)
        {
            string token = operation + " " + Path.GetFullPath(filename).ToLowerInvariant();
            return token;
        }

        public delegate bool CheckoutCallback(string filename);

        public bool DeleteFile(string filename)
        {
            if (filename.Length == 0)
                return false;

            Log.Debug($"Delete '{filename}'");

            if (string.IsNullOrEmpty(_p4FullPath))
                return NotifyUser("could not find p4 exe installed in perforce directory");

            string token = FormatToken("delete", filename);
            if (!LockOp(token))
                return false;

            return AsyncProcess.Schedule(_p4FullPath!, GetUserInfoString(_p4FullPath) + "delete \"" + P4Utils.EscapeP4Path(filename) + "\"", Path.GetDirectoryName(filename), UnlockOp, token);
        }

        public bool AddFile(string filename)
        {
            if (filename.Length == 0)
                return false;

            Log.Debug($"Add '{filename}'");

            if (string.IsNullOrEmpty(_p4FullPath))
                return NotifyUser("could not find p4 exe installed in perforce directory");

            string token = FormatToken("add", filename);
            if (!LockOp(token))
                return false;

            // filename doesn't need escaping when added, even if it contains special characters
            return AsyncProcess.Schedule(_p4FullPath!, GetUserInfoString(_p4FullPath) + "add -f \"" + filename + "\"", Path.GetDirectoryName(filename), UnlockOp, token);
        }

        internal bool Submit(string filename, string? description)
        {
            if (filename.Length == 0)
            {
                Log.Debug("Submit failed due to empty filename");
                return false;
            }

            if (string.IsNullOrEmpty(_p4FullPath))
            {
                Log.Debug($"Submit '{filename}' failed because p4 exe was not found");
                return NotifyUser("could not find p4 exe installed in perforce directory");
            }

            string token = FormatToken("submit", filename);
            if (!LockOp(token))
                return false;

            string submitArguments = string.Empty;
            if (!string.IsNullOrEmpty(description))
            {
                submitArguments = $"-d \"{description}\" ";
            }

            // use Run instead of Schedule because we want to block until the submit is done
            return AsyncProcess.Run(_p4FullPath!, GetUserInfoString(_p4FullPath) + $"submit {submitArguments}\"" + P4Utils.EscapeP4Path(filename) + "\"", Path.GetDirectoryName(filename), UnlockOp, token);
        }

        public bool EditFile(string filename, bool force)
        {
            return Internal_CheckEditFile(f => Internal_EditFile(f, force ? EditFileFlags.Force : EditFileFlags.None), filename);
        }

        public bool EditFileImmediate(string filename, bool force = false)
        {
            return Internal_CheckEditFile(f => Internal_EditFile(f, EditFileFlags.Immediate | (force ? EditFileFlags.Force : EditFileFlags.None)), filename);
        }

        private static bool Internal_CheckEditFile(CheckoutCallback callback, string filename)
        {
            Log.Debug($"Edit '{filename}'");

            bool result = callback(filename);

            void CheckoutAdditionalIfExists(string f)
            {
                if (File.Exists(f))
                    callback(f);
            }

            string ext = Path.GetExtension(filename).ToLowerInvariant();
            switch (ext)
            {
                case ".vcxproj":
                    CheckoutAdditionalIfExists(filename + ".filters");
                    break;
                case ".settings":
                case ".resx":
                    CheckoutAdditionalIfExists(Path.ChangeExtension(filename, ".Designer.cs"));
                    break;
                case ".cs":
                    CheckoutAdditionalIfExists(Path.ChangeExtension(filename, ".Designer.cs"));
                    CheckoutAdditionalIfExists(Path.ChangeExtension(filename, ".resx"));
                    break;
                default:
                    break;
            }

            return result;
        }

        [Flags]
        private enum EditFileFlags
        {
            None = 0,
            Immediate = 1 << 0,
            Force = 1 << 1,
        }

        private bool Internal_EditFile(string filename, EditFileFlags flags)
        {
            if (filename.Length == 0)
            {
                Log.Debug("EditFile failed due to empty filename");
                return false;
            }

            if (!File.Exists(filename))
            {
                Log.Debug($"EditFile '{filename}' failed due to not existing file");
                return false;
            }

            if (!flags.HasFlag(EditFileFlags.Force) && !_ignoreReadOnlyOnEdit && (File.GetAttributes(filename) & FileAttributes.ReadOnly) == 0)
            {
                Log.Info($"EditFile '{filename}' skipped because file was writable. If you want to force calling p4 edit, press the Checkout button in the menus or toggle {nameof(NiftyPerforce.OptionsDialogPage.IgnoreReadOnlyOnEdit)} in the options.");
                return false;
            }

            if (string.IsNullOrEmpty(_p4FullPath))
            {
                Log.Debug($"EditFile '{filename}' failed because p4 exe was not found");
                return NotifyUser("could not find p4 exe installed in perforce directory");
            }

            bool immediate = flags.HasFlag(EditFileFlags.Immediate);
            Log.Debug("EditFile" + (immediate ? "Immediate " : " ") + filename);

            string token = FormatToken("edit", filename);
            if (!LockOp(token))
                return false;

            if (immediate)
                return AsyncProcess.Run(_p4FullPath!, GetUserInfoString(_p4FullPath) + "edit \"" + P4Utils.EscapeP4Path(filename) + "\"", Path.GetDirectoryName(filename), UnlockOp, token);

            return AsyncProcess.Schedule(_p4FullPath!, GetUserInfoString(_p4FullPath) + "edit \"" + P4Utils.EscapeP4Path(filename) + "\"", Path.GetDirectoryName(filename), UnlockOp, token);
        }

        public enum RevertFileOptions
        {
            None,
            OnlyUnchanged,
            DeleteOpenForAdd,
        }

        public bool RevertFile(string filename, RevertFileOptions options)
        {
            if (filename.Length == 0)
                return false;

            if (string.IsNullOrEmpty(_p4FullPath))
                return NotifyUser("could not find p4 exe installed in perforce directory");

            string token = FormatToken("revert", filename);
            if (!LockOp(token))
                return false;

            string revertArguments = string.Empty;
            switch (options)
            {
                case RevertFileOptions.None:
                    break;
                case RevertFileOptions.OnlyUnchanged:
                    revertArguments = "-a ";
                    break;
                case RevertFileOptions.DeleteOpenForAdd:
                    revertArguments = "-w ";
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(options), options, null);
            }

            return AsyncProcess.Schedule(_p4FullPath!, GetUserInfoString(_p4FullPath) + "revert " + revertArguments + "\"" + P4Utils.EscapeP4Path(filename) + "\"", Path.GetDirectoryName(filename), UnlockOp, token);
        }

        public bool DiffFile(string filename)
        {
            if (filename.Length == 0)
                return false;

            if (string.IsNullOrEmpty(_p4FullPath))
                return NotifyUser("could not find p4.exe installed in perforce directory");

            string token = FormatToken("diff", filename);
            if (!LockOp(token))
                return false;

            string? dirname = Path.GetDirectoryName(filename);

            // Let's figure out if the user has some custom diff tool installed. Then we just send whatever we have without any fancy options.
            if (_p4CustomDiff)
                return AsyncProcess.Schedule(_p4FullPath!, GetUserInfoString(_p4FullPath) + " diff \"" + P4Utils.EscapeP4Path(filename) + "#have\"", dirname, UnlockOp, token);

            if (_p4VcDiffHaveSupported)
                return AsyncProcess.Schedule(_p4VcFullPath!, GetUserInfoStringFull(_p4FullPath, true, dirname) + " diffhave \"" + P4Utils.EscapeP4Path(filename) + "\"", Path.GetDirectoryName(_p4VcFullPath), UnlockOp, token, 0);

            // Otherwise let's show a unified diff in the outputpane.
            return UnifiedDiffFile(dirname, filename, token);
        }

        internal bool UnifiedDiffFile(string dirname, string filePath, string? token = null)
        {
            if (string.IsNullOrEmpty(_p4FullPath))
                return NotifyUser("could not find p4 exe installed in perforce directory");

            if (string.IsNullOrEmpty(token))
            {
                token = FormatToken("diff", filePath);
                if (!LockOp(token))
                    return false;
            }

            // Show a unified diff in the outputpane.
            return AsyncProcess.Schedule(_p4FullPath!, GetUserInfoString(_p4FullPath) + " diff -du \"" + P4Utils.EscapeP4Path(filePath) + "#have\"", dirname, UnlockOp, token);
        }

        public bool RevisionHistoryFile(string dirname, string filename)
        {
            if (filename.Length == 0)
                return false;

            if (_p4VcHistorySupported || !string.IsNullOrEmpty(_p4VFullPath))
            {
                string token = FormatToken("history", filename);
                if (!LockOp(token))
                    return false;

                if (_p4VcHistorySupported)
                    return AsyncProcess.Schedule(_p4VcFullPath!, GetUserInfoStringFull(_p4FullPath, true, dirname) + " history \"" + P4Utils.EscapeP4Path(filename) + "\"", Path.GetDirectoryName(_p4VcFullPath), UnlockOp, token, 0);

                if (!string.IsNullOrEmpty(_p4VFullPath))
                    return AsyncProcess.Schedule(_p4VFullPath!, " -win 0 " + GetUserInfoStringFull(_p4FullPath, true, dirname) + " -cmd \"history " + P4Utils.EscapeP4Path(filename) + "\"", Path.GetDirectoryName(_p4VFullPath), UnlockOp, token, 0);
            }

            return NotifyUser("could not find a supported p4vc.exe or p4v.exe installed in perforce directory");
        }

        public bool P4VShowFile(string filename)
        {
            if (filename.Length == 0)
                return false;

            if (_p4VcWorkspaceWindowSupported)
                return AsyncProcess.Schedule(_p4VcFullPath!, GetUserInfoStringFull(_p4FullPath, true, Path.GetDirectoryName(filename)) + " workspacewindow -s \"" + P4Utils.EscapeP4Path(filename) + "\"", Path.GetDirectoryName(_p4VcFullPath), null, null, 0);

            if (!string.IsNullOrEmpty(_p4VFullPath)) // note that the cmd line also accepts -t to open P4V with a specific tab shown
                return AsyncProcess.Schedule(_p4VFullPath!, " -win 0 " + GetUserInfoStringFull(_p4FullPath, true, Path.GetDirectoryName(filename)) + " -s \"" + P4Utils.EscapeP4Path(filename) + "\"", Path.GetDirectoryName(_p4VFullPath), null, null, 0);

            return NotifyUser("could not find p4v.exe installed in perforce directory");
        }

        private string GetUserInfoString(string? p4FullPath)
        {
            return GetUserInfoStringFull(p4FullPath, false, string.Empty);
        }

        private string GetUserInfoStringFull(string? p4FullPath, bool lookup, string? dir)
        {
            // NOTE: This to allow the user to have a P4CONFIG variable and connect to multiple perforce servers seamlessly.
            if (_useSystemEnv)
            {
                if (string.IsNullOrEmpty(p4FullPath))
                {
                    NotifyUser("could not find p4vc in perforce directory");
                }
                else if (lookup && dir != null)
                {
                    SettingsLookupSource[] lookupSources = _preferredLookupSource == SettingsLookupSource.P4Info ?
                        new[] { SettingsLookupSource.P4Info, SettingsLookupSource.P4Set } :
                        new[] { SettingsLookupSource.P4Set, SettingsLookupSource.P4Info };

                    foreach (SettingsLookupSource lookupSource in lookupSources)
                    {
                        switch (lookupSource)
                        {
                            case SettingsLookupSource.P4Info:
                                string? fromP4Info = GetConnectionStringFromP4Info(p4FullPath!, dir);
                                if (!string.IsNullOrEmpty(fromP4Info))
                                    return fromP4Info!;

                                break;

                            case SettingsLookupSource.P4Set:
                                string? fromP4Set = GetConnectionStringFromP4Set(p4FullPath!, dir);
                                if (!string.IsNullOrEmpty(fromP4Set))
                                    return fromP4Set!;

                                break;
                            default:
                                throw new NotImplementedException(lookupSource.ToString());
                        }
                    }
                }

                return string.Empty;
            }

            string arguments = string.Empty;
            if (!string.IsNullOrEmpty(_port))
                arguments += $" -p {_port}";
            if (!string.IsNullOrEmpty(_username))
                arguments += $" -u {_username}";
            if (!string.IsNullOrEmpty(_client))
                arguments += $" -c {_client}";
            arguments += " ";

            Log.Debug("GetUserInfoStringFull : " + arguments);

            return arguments;
        }

        private static string? GetConnectionStringFromP4Set(string p4FullPath, string dir)
        {
            string args = string.Join(
                " ",
                "-s",
                $"-d \"{dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)}\"",
                "set",
                "-q"); // Reduces the output

            string output = Core.Process.Execute(p4FullPath, dir, args);
            return GetConnectionStringFromP4SetOutput(output);
        }

        internal static string? GetConnectionStringFromP4SetOutput(string p4SetOutput)
        {
            if (!string.IsNullOrEmpty(p4SetOutput))
            {
                string? client = null;
                string? server = null;
                string? username = null;
                foreach (string s in p4SetOutput.Split('\n'))
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

                    if (!string.IsNullOrEmpty(client) && !string.IsNullOrEmpty(server) && !string.IsNullOrEmpty(username))
                    {
                        string ret = $"-p {server} -u {username} -c {client}";
                        return ret;
                    }
                }
            }

            return null;
        }

        private static string? GetConnectionStringFromP4Info(string p4FullPath, string dir)
        {
            try
            {
                string args = string.Join(
                    " ",
                    "-s",
                    $"-d \"{dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)}\"",
                    "info");

                string output = Core.Process.Execute(p4FullPath, dir, args);
                var userpattern = new Regex(@"User name: (?<user>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
                var portpattern = new Regex(@"Server address: (?<port>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
                var brokerpattern = new Regex(@"Broker address: (?<port>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
                var proxypattern = new Regex(@"Proxy address: (?<port>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
                var clientpattern = new Regex(@"Client name: (?<client>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);

                Match usermatch = userpattern.Match(output);
                Match portmatch = portpattern.Match(output);
                Match brokermatch = brokerpattern.Match(output);
                Match proxymatch = proxypattern.Match(output);
                Match clientmatch = clientpattern.Match(output);

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

                Match encryptionmatch = encryptionpattern.Match(output);
                bool encrypted = encryptionmatch.Success && encryptionmatch.Groups["encrypted"].Value.Trim() == "encrypted";
                if (encrypted)
                {
                    server = $"ssl:{server}";
                }

                string ret = $" -p {server} -u {username} -c {client} ";

                Log.Debug("GetUserInfoStringFull : " + ret);

                return ret;
            }
            catch
            {
                Log.Error("Failed to execute info string discovery");
            }

            return null;
        }

        public bool TimeLapseView(string dirname, string filename)
        {
            if (string.IsNullOrEmpty(_p4VcFullPath))
                return NotifyUser("could not find p4vc in perforce directory");

            string arguments = GetUserInfoStringFull(_p4FullPath, true, dirname);
            arguments += " tlv \"" + filename + "\"";

            string token = FormatToken("timelapse", filename);
            if (!LockOp(token))
                return false;

            return AsyncProcess.Schedule(_p4VcFullPath!, arguments, Path.GetDirectoryName(_p4VcFullPath), UnlockOp, token, 0);
        }

        public bool RevisionGraph(string dirname, string filename)
        {
            if (string.IsNullOrEmpty(_p4VcFullPath))
                return NotifyUser("could not find p4vc in perforce directory");

            string arguments = GetUserInfoStringFull(_p4FullPath, true, dirname);
            arguments += " revisiongraph \"" + filename + "\"";

            string token = FormatToken("revisiongraph", filename);
            if (!LockOp(token))
                return false;

            return AsyncProcess.Schedule(_p4VcFullPath!, arguments, Path.GetDirectoryName(_p4VcFullPath), UnlockOp, token, 0);
        }

        private static string? GetRegistryValue(string key, string value, bool global)
        {
            Microsoft.Win32.RegistryKey? hklm = Microsoft.Win32.Registry.LocalMachine;
            if (!global)
                hklm = Microsoft.Win32.Registry.CurrentUser;
            hklm = hklm.OpenSubKey(key);
            if (hklm == null)
            {
                Log.Debug("Could not find registry key " + (global ? "HKLM\\" : "HKCU\\") + key);
                return null;
            }

            object? regValue = hklm.GetValue(value);
            if (regValue == null)
            {
                Log.Debug("Could not find registry value " + value + " in " + (global ? "HKLM\\" : "HKCU\\") + key);
                return null;
            }

            return (string)regValue;
        }

        public void CheckInstalledFiles(P4Utils p4Utils)
        {
            Log.Debug("Looking for installed files...");

            _p4CustomDiff = false;

            _p4FullPath = p4Utils.LocateP4InstallPath(P4Utils.P4ExeName);
            _p4VFullPath = p4Utils.LocateP4InstallPath(P4Utils.P4VExeName);
            _p4VcFullPath = p4Utils.LocateP4InstallPath(P4Utils.P4VcBatName);
            _p4VcFullPath ??= p4Utils.LocateP4InstallPath(P4Utils.P4VcExeName);

            if (_p4FullPath != null)
                Log.Info("Found perforce installation at {0}", Path.GetDirectoryName(_p4FullPath) ?? string.Empty);

            Log.Info("[{0}] {1}", _p4FullPath != null ? "X" : " ", _p4FullPath ?? P4Utils.P4ExeName);
            Log.Info("[{0}] {1}", _p4VFullPath != null ? "X" : " ", _p4VFullPath ?? P4Utils.P4VExeName);
            Log.Info("[{0}] {1}", _p4VcFullPath != null ? "X" : " ", _p4VcFullPath ?? "p4vc(.bat|.exe)");

            string? p4diff = GetRegistryValue("SOFTWARE\\Perforce\\Environment", "P4DIFF", true);
            if (!string.IsNullOrEmpty(p4diff))
            {
                Log.Info("[X] p4 custom diff '{0}' from HKLM", p4diff!);
                _p4CustomDiff = true;
            }

            p4diff = GetRegistryValue("SOFTWARE\\Perforce\\Environment", "P4DIFF", false);
            if (!string.IsNullOrEmpty(p4diff))
            {
                Log.Info("[X] p4 custom diff '{0}' from HKCU", p4diff!);
                _p4CustomDiff = true;
            }

            p4diff = Environment.GetEnvironmentVariable("P4DIFF");
            if (p4diff != null)
            {
                Log.Info("[X] p4 custom diff '{0}' from P4DIFF env var", p4diff);
                _p4CustomDiff = true;
            }

            if (!_p4CustomDiff)
                Log.Info("[ ] p4 custom diff");

            p4Utils.DetermineSupportedP4VFeatures(_p4VFullPath, out _p4VcWorkspaceWindowSupported, out _p4VcDiffHaveSupported, out _p4VcHistorySupported);
        }

        private static bool NotifyUser(string message)
        {
            if (!s_alreadyNotified.Contains(message))
            {
                System.Windows.Forms.MessageBox.Show(message, "NiftyPerforce Notice!", System.Windows.Forms.MessageBoxButtons.OK);
                s_alreadyNotified.Add(message);
            }

            return false;
        }

        public string RemapToMain(string filename, string mainline)
        {
            Log.Debug("RemapToMain : {0} {1}", filename, mainline);

            if (mainline.Length == 0)
            {
                Log.Error("Tried to find the mainline version of {0}, but the mainline path spec is empty", filename);
                throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "Tried to find the mainline version of {0}, but the mainline path spec is empty", filename));
            }

            string result = Core.Process.Execute(_p4FullPath!, Path.GetDirectoryName(filename), GetUserInfoString(_p4FullPath) + "integrated \"" + P4Utils.EscapeP4Path(filename) + "\"");
            result = P4Utils.UnEscapeP4Path(result);

            var pattern = new Regex(@"//(.*)#\d+ - .*//([^#]+)#\d+", RegexOptions.Compiled);

            foreach (Match m in pattern.Matches(result))
            {
                string candidate = "//" + m.Groups[2];

                if (candidate.StartsWith(mainline, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            return filename;
        }
    }
}
