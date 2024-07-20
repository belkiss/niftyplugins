// Copyright (C) 2006-2017 Jim Tilander, 2017-2024 Lambert Clara. See the COPYING file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Aurora;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace NiftyPerforce
{
    // Simplification wrapper around running perforce commands.
    internal static class P4Operations
    {
        private const string P4vcBatFileName = "p4vc.bat";

        private static readonly object s_opsInFlightLock = new object();
        private static readonly HashSet<string> s_opsInFlight = new HashSet<string>();
        private static readonly HashSet<string> s_alreadyNotified = new HashSet<string>();

        private static bool s_p4Installed;
        private static bool s_p4CustomDiff;
        private static string? s_p4vcExeName;
        private static string? s_p4vDir;
        private static string? s_p4vcDir;

        private static bool s_p4vcHistorySupported;
        private static bool s_p4vcDiffHaveSupported;
        private static bool s_p4vcWorkspaceWindowSupported;

        private static bool LockOp(string token)
        {
            bool added = false;
            lock (s_opsInFlightLock)
            {
                added = s_opsInFlight.Add(token);
            }

            if (added)
            {
                Log.Debug("## Locked \"" + token + "\"");
                return true;
            }
            else
            {
                Log.Error(token + " already in progress");
                return false;
            }
        }

        private static void UnlockOp(bool ok, object? token_)
        {
            string? token = token_ as string;
            Trace.Assert(token != null, $"{nameof(UnlockOp)} must be called with a string token");

            bool removed = false;
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

        private static string EscapeP4Path(string filename)
        {
            return filename.Replace("%", "%25").Replace("#", "%23").Replace("@", "%40");
        }

        private static string UnEscapeP4Path(string escapedfilename)
        {
            return escapedfilename.Replace("%40", "@").Replace("%23", "#").Replace("%25", "%");
        }

        private static string FormatToken(string operation, string filename)
        {
            string token = operation + " " + Path.GetFullPath(filename).ToLowerInvariant();
            return token;
        }

        public delegate bool CheckoutCallback(string filename);

        public static bool DeleteFile(string filename)
        {
            if (filename.Length == 0)
                return false;

            Log.Debug($"Delete '{filename}'");

            if (!s_p4Installed)
                return NotifyUser("could not find p4 exe installed in perforce directory");

            string token = FormatToken("delete", filename);
            if (!LockOp(token))
                return false;
            return AsyncProcess.Schedule("p4.exe", GetUserInfoString() + "delete \"" + EscapeP4Path(filename) + "\"", Path.GetDirectoryName(filename), new AsyncProcess.OnDone(UnlockOp), token);
        }

        public static bool AddFile(string filename)
        {
            if (filename.Length == 0)
                return false;

            Log.Debug($"Add '{filename}'");

            if (!s_p4Installed)
                return NotifyUser("could not find p4 exe installed in perforce directory");

            string token = FormatToken("add", filename);
            if (!LockOp(token))
                return false;

            return AsyncProcess.Schedule("p4.exe", GetUserInfoString() + "add -f \"" + filename + "\"", Path.GetDirectoryName(filename), new AsyncProcess.OnDone(UnlockOp), token);
        }

        public static bool EditFile(string filename, bool force)
        {
            return Internal_CheckEditFile(new CheckoutCallback((string f) => Internal_EditFile(f, force ? EditFileFlags.Force : EditFileFlags.None)), filename);
        }

        public static bool EditFileImmediate(string filename)
        {
            return Internal_CheckEditFile(new CheckoutCallback((string f) => Internal_EditFile(f, EditFileFlags.Immediate)), filename);
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
            if (ext == ".vcxproj")
            {
                CheckoutAdditionalIfExists(filename + ".filters");
            }

            if (ext == ".settings" || ext == ".resx")
            {
                CheckoutAdditionalIfExists(Path.ChangeExtension(filename, ".Designer.cs"));
            }

            if (ext == ".cs")
            {
                CheckoutAdditionalIfExists(Path.ChangeExtension(filename, ".Designer.cs"));
                CheckoutAdditionalIfExists(Path.ChangeExtension(filename, ".resx"));
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

        private static bool Internal_EditFile(string filename, EditFileFlags flags)
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

            if (!flags.HasFlag(EditFileFlags.Force) && !Singleton<NiftyPerforce.OptionsDialogPage>.Instance.IgnoreReadOnlyOnEdit && ((File.GetAttributes(filename) & FileAttributes.ReadOnly) == 0))
            {
                Log.Info($"EditFile '{filename}' failed because file was not read only. If you want to force calling p4 edit, press the Checkout button in the menus or toggle {nameof(OptionsDialogPage.IgnoreReadOnlyOnEdit)} in the options.");
                return false;
            }

            if (!s_p4Installed)
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
                return AsyncProcess.Run("p4.exe", GetUserInfoString() + "edit \"" + EscapeP4Path(filename) + "\"", Path.GetDirectoryName(filename), new AsyncProcess.OnDone(UnlockOp), token);

            return AsyncProcess.Schedule("p4.exe", GetUserInfoString() + "edit \"" + EscapeP4Path(filename) + "\"", Path.GetDirectoryName(filename), new AsyncProcess.OnDone(UnlockOp), token);
        }

        public static bool RevertFile(string filename, bool onlyUnchanged)
        {
            if (filename.Length == 0)
                return false;
            if (!s_p4Installed)
                return NotifyUser("could not find p4 exe installed in perforce directory");

            string token = FormatToken("revert", filename);
            if (!LockOp(token))
                return false;

            string revertArguments = onlyUnchanged ? "-a " : string.Empty;
            return AsyncProcess.Schedule("p4.exe", GetUserInfoString() + "revert " + revertArguments + "\"" + EscapeP4Path(filename) + "\"", Path.GetDirectoryName(filename), new AsyncProcess.OnDone(UnlockOp), token);
        }

        public static bool DiffFile(string filename)
        {
            if (filename.Length == 0)
                return false;

            if (!s_p4Installed)
                return NotifyUser("could not find p4.exe installed in perforce directory");

            string token = FormatToken("diff", filename);
            if (!LockOp(token))
                return false;

            string? dirname = Path.GetDirectoryName(filename);

            // Let's figure out if the user has some custom diff tool installed. Then we just send whatever we have without any fancy options.
            if (s_p4CustomDiff)
                return AsyncProcess.Schedule("p4.exe", GetUserInfoString() + " diff \"" + EscapeP4Path(filename) + "#have\"", dirname, new AsyncProcess.OnDone(UnlockOp), token);

            if (s_p4vcDiffHaveSupported)
                return AsyncProcess.Schedule(s_p4vcExeName!, GetUserInfoStringFull(true, dirname) + " diffhave \"" + filename + "\"", s_p4vcDir!, new AsyncProcess.OnDone(UnlockOp), token, 0);

            // Otherwise let's show a unified diff in the outputpane.
            return AsyncProcess.Schedule("p4.exe", GetUserInfoString() + " diff -du \"" + EscapeP4Path(filename) + "#have\"", dirname, new AsyncProcess.OnDone(UnlockOp), token);
        }

        public static bool RevisionHistoryFile(string dirname, string filename)
        {
            if (filename.Length == 0)
                return false;

            if (s_p4vcHistorySupported || !string.IsNullOrEmpty(s_p4vDir))
            {
                string token = FormatToken("history", filename);
                if (!LockOp(token))
                    return false;

                if (s_p4vcHistorySupported)
                    return AsyncProcess.Schedule(s_p4vcExeName!, GetUserInfoStringFull(true, dirname) + " history \"" + filename + "\"", s_p4vcDir!, new AsyncProcess.OnDone(UnlockOp), token, 0);

                if (!string.IsNullOrEmpty(s_p4vDir))
                    return AsyncProcess.Schedule("p4v.exe", " -win 0 " + GetUserInfoStringFull(true, dirname) + " -cmd \"history " + EscapeP4Path(filename) + "\"", s_p4vDir!, new AsyncProcess.OnDone(UnlockOp), token, 0);
            }

            return NotifyUser("could not find a supported p4vc.exe or p4v.exe installed in perforce directory");
        }

        public static bool P4VShowFile(string filename)
        {
            if (filename.Length == 0)
                return false;

            if (s_p4vcWorkspaceWindowSupported)
                return AsyncProcess.Schedule(s_p4vcExeName!, GetUserInfoStringFull(true, Path.GetDirectoryName(filename)) + " workspacewindow -s \"" + filename + "\"", s_p4vcDir!, null, null, 0);

            if (!string.IsNullOrEmpty(s_p4vDir)) // note that the cmd line also accepts -t to open P4V with a specific tab shown
                return AsyncProcess.Schedule("p4v.exe", " -win 0 " + GetUserInfoStringFull(true, Path.GetDirectoryName(filename)) + " -s \"" + filename + "\"", s_p4vDir!, null, null, 0);

            return NotifyUser("could not find p4v.exe installed in perforce directory");
        }

        private static string GetUserInfoString()
        {
            return GetUserInfoStringFull(false, string.Empty);
        }

        private static string GetUserInfoStringFull(bool lookup, string? dir)
        {
            OptionsDialogPage config = Singleton<OptionsDialogPage>.Instance;

            // NOTE: This to allow the user to have a P4CONFIG variable and connect to multiple perforce servers seamlessly.
            if (config.UseSystemEnv)
            {
                if (lookup && dir != null)
                {
                    SettingsLookupSource[] lookupSources = (config.PreferredLookupSource == SettingsLookupSource.P4Set) ?
                        new SettingsLookupSource[] { SettingsLookupSource.P4Set, SettingsLookupSource.P4Info } :
                        new SettingsLookupSource[] { SettingsLookupSource.P4Info, SettingsLookupSource.P4Set };

                    foreach (SettingsLookupSource lookupSource in lookupSources)
                    {
                        switch (lookupSource)
                        {
                            case SettingsLookupSource.P4Info:
                                string? fromP4Info = GetConnectionStringFromP4Info(dir);
                                if (!string.IsNullOrEmpty(fromP4Info))
                                    return fromP4Info!;

                                break;

                            case SettingsLookupSource.P4Set:
                                string? fromP4Set = GetConnectionStringFromP4Set(dir);
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
            arguments += " -p " + config.Port;
            arguments += " -u " + config.Username;
            arguments += " -c " + config.Client;
            arguments += " ";

            Log.Debug("GetUserInfoStringFull : " + arguments);

            return arguments;
        }

        private static string? GetConnectionStringFromP4Set(string dir)
        {
            string args = string.Join(
                " ",
                "-s",
                $"-L \"{dir}\"",
                "set",
                "-q"); // Reduces the output

            string output = Aurora.Process.Execute("p4", dir, args);
            return GetConnectionStringFromP4SetOutput(output);
        }

        internal static string? GetConnectionStringFromP4SetOutput(string p4setOutput)
        {
            if (!string.IsNullOrEmpty(p4setOutput))
            {
                string? client = null;
                string? server = null;
                string? username = null;
                foreach (string s in p4setOutput.Split('\n'))
                {
                    string trim = s.Trim();
                    if (trim.StartsWith("P4CLIENT="))
                    {
                        client = trim.Substring(9);
                    }
                    else if (trim.StartsWith("P4PORT="))
                    {
                        server = trim.Substring(7);
                    }
                    else if (trim.StartsWith("P4USER="))
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

        private static string? GetConnectionStringFromP4Info(string dir)
        {
            try
            {
                string output = Aurora.Process.Execute("p4", dir, $"-s -L \"{dir}\" info");
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
            catch (Aurora.ProcessException e)
            {
                Log.Error("Failed to execute info string discovery: {0}", e.Message);
            }

            return null;
        }

        public static bool TimeLapseView(string dirname, string filename)
        {
            if (string.IsNullOrEmpty(s_p4vcExeName))
                return NotifyUser("could not find p4vc in perforce directory");

            string arguments = GetUserInfoStringFull(true, dirname);
            arguments += " tlv \"" + filename + "\"";

            string token = FormatToken("timelapse", filename);
            if (!LockOp(token))
                return false;

            return AsyncProcess.Schedule(s_p4vcExeName!, arguments, s_p4vcDir!, new AsyncProcess.OnDone(UnlockOp), token, 0);
        }

        public static bool RevisionGraph(string dirname, string filename)
        {
            if (string.IsNullOrEmpty(s_p4vcExeName))
                return NotifyUser("could not find p4vc in perforce directory");

            string arguments = GetUserInfoStringFull(true, dirname);
            arguments += " revisiongraph \"" + filename + "\"";

            string token = FormatToken("revisiongraph", filename);
            if (!LockOp(token))
                return false;

            return AsyncProcess.Schedule(s_p4vcExeName!, arguments, s_p4vcDir!, new AsyncProcess.OnDone(UnlockOp), token, 0);
        }

        public static string? GetRegistryValue(string key, string value, bool global)
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

        private static bool LookupP4VC(Func<string, bool> check)
        {
            // starting with 2021.1/2075061
            //    #105247 (Change #2069769)
            //      The p4vc.exe executable has been removed from the Windows installers.
            //      To start P4VC, use the p4vc.bat script.
            foreach (string? candidateName in new[] { P4vcBatFileName, "p4vc.exe" })
            {
                if (check(candidateName))
                {
                    s_p4vcExeName = candidateName;
                    return true;
                }
            }

            return false;
        }

        public static void CheckInstalledFiles()
        {
            Log.Debug("Looking for installed files...");
            s_p4Installed = false;
            s_p4CustomDiff = false;
            s_p4vcExeName = null;
            s_p4vDir = null;
            s_p4vcDir = null;
            string? p4diff = null;

            // Let's try the default 64 bit installation. Since we are in a 32 bit exe this is tricky
            // to ask the registry...
            string? installRoot = null;
            string candidate = @"C:\Program Files\Perforce";
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "p4.exe")))
            {
                installRoot = candidate;
            }

            if (installRoot == null)
            {
                installRoot = GetRegistryValue("SOFTWARE\\Perforce\\Environment", "P4INSTROOT", true);

                // Perhaps it's an older installation?
                // http://code.google.com/p/niftyplugins/issues/detail?id=47&can=1&q=path
                installRoot ??= GetRegistryValue("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths", "p4.exe", true);
            }

            if (installRoot != null)
            {
                Log.Info("Found perforce installation at {0}", installRoot);

                s_p4Installed = File.Exists(Path.Combine(installRoot, "p4.exe"));
                if (File.Exists(Path.Combine(installRoot, "p4v.exe")))
                    s_p4vDir = installRoot;

                if (LookupP4VC((candidateName) => File.Exists(Path.Combine(installRoot, candidateName))))
                    s_p4vcDir = installRoot;

                Log.Info("[{0}] p4.exe", s_p4Installed ? "X" : " ");
                Log.Info("[{0}] p4v.exe", !string.IsNullOrEmpty(s_p4vDir) ? "X" : " ");
                Log.Info("[{0}] {1}", s_p4vcExeName != null ? "X" : " ", s_p4vcExeName ?? "p4vc(.bat|.exe)");

                p4diff = GetRegistryValue("SOFTWARE\\Perforce\\Environment", "P4DIFF", true);
                if (!string.IsNullOrEmpty(p4diff))
                {
                    Log.Info("[X] p4 custom diff '{0}' from HKLM", p4diff!);
                    s_p4CustomDiff = true;
                }

                p4diff = GetRegistryValue("SOFTWARE\\Perforce\\Environment", "P4DIFF", false);
                if (!string.IsNullOrEmpty(p4diff))
                {
                    Log.Info("[X] p4 custom diff '{0}' from HKCU", p4diff!);
                    s_p4CustomDiff = true;
                }

                p4diff = Environment.GetEnvironmentVariable("P4DIFF");
                if (p4diff != null)
                {
                    Log.Info("[X] p4 custom diff '{0}' from P4DIFF env var", p4diff);
                    s_p4CustomDiff = true;
                }

                if (!s_p4CustomDiff)
                    Log.Info("[ ] p4 custom diff");
            }
            else
            {
                // Let's try to find the executables through the path variable instead.
                if (Help.FindFileInPath("p4.exe") != null)
                {
                    s_p4Installed = true;
                    Log.Info("Found p4 in path");
                }

                s_p4vDir = Help.FindFileInPath("p4v.exe");
                if (s_p4vDir != null)
                {
                    Log.Info("Found p4v in path");
                }

                string? p4vc_dir = null;
                if (LookupP4VC((candidateName) => (p4vc_dir = Help.FindFileInPath(candidateName)) != null))
                {
                    s_p4vcDir = p4vc_dir;
                    Log.Info("Found {0} in path", s_p4vcExeName!);
                }

                Log.Warning("Could not find any peforce installation in the registry!!!");

                p4diff = Environment.GetEnvironmentVariable("P4DIFF");
                if (p4diff != null)
                {
                    Log.Info("Found p4 custom diff");
                    s_p4CustomDiff = true;
                }
            }

            DetermineSupportedFeatures();
        }

        private static void DetermineSupportedFeatures()
        {
            bool p4vcBat = s_p4vcExeName == P4vcBatFileName;

            // workspacewindow was added in p4v 2023.2/2443448, and 2024.1/2573667 deprecated p4v -s and p4v -t
            s_p4vcWorkspaceWindowSupported = p4vcBat && P4VCHasCommand("workspacewindow");
            Log.Info("[{0}] p4vc workspacewindow", s_p4vcWorkspaceWindowSupported ? "X" : " ");

            // since p4vc.bat was introduced with 2021.1/2075061, if we have it we know we have diffhave, hence history

            // diffhave was added in p4v 2020.1/1946989
            s_p4vcDiffHaveSupported = p4vcBat || P4VCHasCommand("diffhave");
            Log.Info("[{0}] p4vc diffhave", s_p4vcDiffHaveSupported ? "X" : " ");

            // history was added in p4v 2019.2 update1/1883366
            // so if we have diffhave we know we have history and can skip the test
            s_p4vcHistorySupported = s_p4vcDiffHaveSupported || P4VCHasCommand("history");
            Log.Info("[{0}] p4vc history", s_p4vcHistorySupported ? "X" : " ");
        }

        private static bool P4VCHasCommand(string command)
        {
            if (string.IsNullOrEmpty(s_p4vcExeName))
                return false;

            string result = Aurora.Process.Execute(s_p4vcExeName!, string.Empty, $"help {command}", throwIfNonZeroExitCode: false);

            return result.IndexOf("Invalid help command request...", StringComparison.Ordinal) == -1;
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

        public static string RemapToMain(string filename, string mainline)
        {
            Log.Debug("RemapToMain : {0} {1}", filename, mainline);

            if (mainline.Length == 0)
            {
                Log.Error("Tried to find the mainline version of {0}, but the mainline path spec is empty", filename);
                throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "Tried to find the mainline version of {0}, but the mainline path spec is empty", filename));
            }

            string result = Aurora.Process.Execute("p4.exe", Path.GetDirectoryName(filename), GetUserInfoString() + "integrated \"" + EscapeP4Path(filename) + "\"");
            result = UnEscapeP4Path(result);

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
