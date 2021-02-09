// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using System;
using EnvDTE;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Aurora;

namespace NiftyPerforce
{
	// Simplification wrapper around running perforce commands.
	class P4Operations
	{
		private static bool g_p4installed = false;
		private static bool g_p4vinstalled = false;
		private static bool g_p4customdiff = false;
		private static string g_p4vc_exename = null;

		private static bool g_p4vc_history_supported = false;
		private static bool g_p4vc_diffhave_supported = false;

		private static Dictionary<string, bool> g_opsInFlight = new Dictionary<string, bool>();

		private static HashSet<string> g_alreadyNotified = new HashSet<string>();

		private static bool LockOp(string token)
		{
			try
			{
				lock (g_opsInFlight)
				{
					g_opsInFlight.Add(token, true);
				}
				Log.Debug("## Locked \"" + token + "\"" );
				return true;
			}
			catch(ArgumentException)
			{
				//Log.Debug("!! Failed to lock \"" + token + "\"");
				Log.Error(token + " already in progress");
				return false;
			}
		}

		private static void UnlockOp(bool ok, object token_)
		{
			string token = (string)token_;
			try
			{
				lock (g_opsInFlight)
				{
					if (g_opsInFlight.Remove(token))
					{
						Log.Debug("## Unlocked \"" + token + "\"");
					}
					else
					{
						Log.Debug("!! Failed to unlock \"" + token + "\"");
					}
				}
			}
			catch (ArgumentNullException)
			{
			}
		}

		private static string FormatToken(string operation, string filename)
		{
			string token = operation + " " + Path.GetFullPath(filename).ToLowerInvariant();
			return token;
		}

		public delegate bool CheckoutCallback(OutputWindowPane output, string filename);

		public static bool DeleteFile(OutputWindowPane output, string filename)
		{
			if(filename.Length == 0)
				return false;
			if(!g_p4installed)
				return NotifyUser("could not find p4 exe installed in perforce directory");

			string token = FormatToken("delete", filename);
			if (!LockOp(token))
				return false;
			return AsyncProcess.Schedule(output, "p4.exe", GetUserInfoString() + "delete \"" + filename + "\"", Path.GetDirectoryName(filename), new AsyncProcess.OnDone(UnlockOp), token);
		}

		public static bool AddFile(OutputWindowPane output, string filename)
		{
			if(filename.Length == 0)
				return false;
			if(!g_p4installed)
				return NotifyUser("could not find p4 exe installed in perforce directory");

			string token = FormatToken("add", filename);
			if (!LockOp(token))
				return false;
			return AsyncProcess.Schedule(output, "p4.exe", GetUserInfoString() + "add \"" + filename + "\"", Path.GetDirectoryName(filename), new AsyncProcess.OnDone(UnlockOp), token);
		}

		public static bool EditFile(OutputWindowPane output, string filename)
		{
			return Internal_CheckEditFile(new CheckoutCallback(Internal_EditFile), output, filename);
		}

		public static bool EditFileImmediate(OutputWindowPane output, string filename)
		{
			return Internal_CheckEditFile(new CheckoutCallback(Internal_EditFileImmediate), output, filename);
		}

		private static bool Internal_CheckEditFile(CheckoutCallback callback, OutputWindowPane output, string filename)
		{
			Log.Debug($"Edit '{filename}'");
			bool result = callback(output, filename);

			string ext = Path.GetExtension(filename).ToLowerInvariant();
			if (ext == ".vcxproj")
			{
				callback(output, filename + ".filters");
			}

			if (ext == ".settings" || ext == ".resx")
			{
				callback(output, Path.ChangeExtension(filename, ".Designer.cs"));
			}

			if (ext == ".cs")
			{
				callback(output, Path.ChangeExtension(filename, ".Designer.cs"));
				callback(output, Path.ChangeExtension(filename, ".resx"));
			}

			return result;
		}

		private static bool Internal_EditFile(OutputWindowPane output, string filename)
		{
			return Internal_EditFile(output, filename, false);
		}

		private static bool Internal_EditFileImmediate(OutputWindowPane output, string filename)
		{
			return Internal_EditFile(output, filename, true);
		}

		private static bool Internal_EditFile(OutputWindowPane output, string filename, bool immediate)
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
			if (!Singleton<NiftyPerforce.Config>.Instance.IgnoreReadOnlyOnEdit && (0 == (File.GetAttributes(filename) & FileAttributes.ReadOnly)))
			{
				Log.Debug($"EditFile '{filename}' failed because file was not read only. If you want to force calling p4 edit, toggle IgnoreReadOnlyOnEdit in the options.");
				return false;
			}
			if (!g_p4installed)
			{
				Log.Debug($"EditFile '{filename}' failed because p4 exe was not found");
				return NotifyUser("could not find p4 exe installed in perforce directory");
			}

			Log.Debug("EditFile" + (immediate ? "Immediate " : " ") + filename);
			string token = FormatToken("edit", filename);
			if (!LockOp(token))
				return false;

			if (immediate)
				return AsyncProcess.Run(output, "p4.exe", GetUserInfoString() + "edit \"" + filename + "\"", Path.GetDirectoryName(filename), new AsyncProcess.OnDone(UnlockOp), token);

			return AsyncProcess.Schedule(output, "p4.exe", GetUserInfoString() + "edit \"" + filename + "\"", Path.GetDirectoryName(filename), new AsyncProcess.OnDone(UnlockOp), token);
		}

		public static bool RevertFile(OutputWindowPane output, string filename, bool onlyUnchanged)
		{
			if(filename.Length == 0)
				return false;
			if(!g_p4installed)
				return NotifyUser("could not find p4 exe installed in perforce directory");

			string token = FormatToken("revert", filename);
			if (!LockOp(token))
				return false;

			string revertArguments = onlyUnchanged ? "-a " : string.Empty;
			return AsyncProcess.Schedule(output, "p4.exe", GetUserInfoString() + "revert " + revertArguments + "\"" + filename + "\"", Path.GetDirectoryName(filename), new AsyncProcess.OnDone(UnlockOp), token);
		}

		public static bool DiffFile(OutputWindowPane output, string filename)
		{
			if(filename.Length == 0)
				return false;

			if (!g_p4installed)
				return NotifyUser("could not find p4.exe installed in perforce directory");

			string token = FormatToken("diff", filename);
			if (!LockOp(token))
				return false;

			string dirname = Path.GetDirectoryName(filename);

			// Let's figure out if the user has some custom diff tool installed. Then we just send whatever we have without any fancy options.
			if (g_p4customdiff)
				return AsyncProcess.Schedule(output, "p4.exe", GetUserInfoString() + " diff \"" + filename + "#have\"", dirname, new AsyncProcess.OnDone(UnlockOp), token);

			if (g_p4vc_diffhave_supported)
				return AsyncProcess.Schedule(output, g_p4vc_exename, GetUserInfoStringFull(true, dirname) + " diffhave \"" + filename + "\"", dirname, new AsyncProcess.OnDone(UnlockOp), token, 0);

			// Otherwise let's show a unified diff in the outputpane.
			return AsyncProcess.Schedule(output, "p4.exe", GetUserInfoString() + " diff -du \"" + filename + "#have\"", dirname, new AsyncProcess.OnDone(UnlockOp), token);
		}

		public static bool RevisionHistoryFile(OutputWindowPane output, string dirname, string filename)
		{
			if(filename.Length == 0)
				return false;
			string token = FormatToken("history", filename);
			if (!LockOp(token))
				return false;
			if(g_p4vc_history_supported)
				return AsyncProcess.Schedule(output, g_p4vc_exename, GetUserInfoStringFull(true, dirname) + " history \"" + filename + "\"", dirname, new AsyncProcess.OnDone(UnlockOp), token, 0);
			if(g_p4vinstalled)
				return AsyncProcess.Schedule(output, "p4v.exe", " -win 0 " + GetUserInfoStringFull(true, dirname) + " -cmd \"history " + filename + "\"", dirname, new AsyncProcess.OnDone(UnlockOp), token, 0);
			return NotifyUser("could not find a supported p4vc.exe or p4v.exe installed in perforce directory");
		}

		public static bool P4VShowFile(OutputWindowPane output, string filename)
		{
			if(filename.Length == 0)
				return false;
			if (g_p4vinstalled) // note that the cmd line also accepts -t to open P4V with a specific tab shown
				return AsyncProcess.Schedule(output, "p4v.exe", " -win 0 " + GetUserInfoStringFull(true, Path.GetDirectoryName(filename)) + " -s \"" + filename + "\"", Path.GetDirectoryName(filename), null, null, 0);
			return NotifyUser("could not find p4v.exe installed in perforce directory");
		}

		private static string GetUserInfoString()
		{
			return GetUserInfoStringFull(false, "");
		}

		private static string GetUserInfoStringFull(bool lookup, string dir)
		{
			// NOTE: This to allow the user to have a P4CONFIG variable and connect to multiple perforce servers seamlessly.
			if (Singleton<NiftyPerforce.Config>.Instance.UseSystemEnv)
			{
				if(lookup)
				{
					try
					{
						string output = Aurora.Process.Execute("p4", dir, $"-s -L \"{dir}\" info");
						Regex userpattern = new Regex(@"User name: (?<user>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
						Regex portpattern = new Regex(@"Server address: (?<port>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
						Regex brokerpattern = new Regex(@"Broker address: (?<port>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
						Regex clientpattern = new Regex(@"Client name: (?<client>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);

						Match usermatch = userpattern.Match(output);
						Match portmatch = portpattern.Match(output);
						Match brokermatch = brokerpattern.Match(output);
						Match clientmatch = clientpattern.Match(output);

						string port = portmatch.Groups["port"].Value.Trim();
						string broker = brokermatch.Groups["port"].Value.Trim();
						string username = usermatch.Groups["user"].Value.Trim();
						string client = clientmatch.Groups["client"].Value.Trim();

						string server = broker;
						if (string.IsNullOrEmpty(server))
							server = port;

						string ret = $" -p {server} -u {username} -c {client} ";

						Log.Debug("GetUserInfoStringFull : " + ret);

						return ret;
					}
					catch (Aurora.Process.Error e)
					{
						Log.Error("Failed to execute info string discovery: {0}", e.info);
					}
				}

				return "";
			}

			var config = Singleton<NiftyPerforce.Config>.Instance;
			string arguments = "";
			arguments += " -p " + config.Port;
			arguments += " -u " + config.Username;
			arguments += " -c " + config.Client;
			arguments += " ";

			Log.Debug("GetUserInfoStringFull : " + arguments);

			return arguments;
		}

		public static bool TimeLapseView(OutputWindowPane output, string dirname, string filename)
		{
			if(string.IsNullOrEmpty(g_p4vc_exename))
				return NotifyUser("could not find p4vc in perforce directory");

			string arguments = GetUserInfoStringFull(true, dirname);
			arguments += " tlv \"" + filename + "\"";


			string token = FormatToken("timelapse", filename);
			if (!LockOp(token))
				return false;
			return AsyncProcess.Schedule(output, g_p4vc_exename, arguments, dirname, new AsyncProcess.OnDone(UnlockOp), token, 0);
		}

		public static bool RevisionGraph(OutputWindowPane output, string dirname, string filename)
		{
			if (string.IsNullOrEmpty(g_p4vc_exename))
				return NotifyUser("could not find p4vc in perforce directory");

			string arguments = GetUserInfoStringFull(true, dirname);
			arguments += " revisiongraph \"" + filename + "\"";

			string token = FormatToken("revisiongraph", filename);
			if (!LockOp(token))
				return false;
			return AsyncProcess.Schedule(output, g_p4vc_exename, arguments, dirname, new AsyncProcess.OnDone(UnlockOp), token, 0);
		}

		public static string GetRegistryValue(string key, string value, bool global)
		{
			Microsoft.Win32.RegistryKey hklm = Microsoft.Win32.Registry.LocalMachine;
			if(!global)
				hklm = Microsoft.Win32.Registry.CurrentUser;
			hklm = hklm.OpenSubKey(key);
			if(null == hklm)
			{
				Log.Debug("Could not find registry key " + (global ? "HKLM\\" : "HKCU\\") + key);
				return null;
			}
			Object regValue = hklm.GetValue(value);
			if(null == regValue)
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
			//		The p4vc.exe executable has been removed from the Windows installers.
			//		To start P4VC, use the p4vc.bat script.
			foreach (var candidateName in new[] { "p4vc.bat", "p4vc.exe" })
			{
				if (check(candidateName))
				{
					g_p4vc_exename = candidateName;
					return true;
				}
			}
			return false;
		}

		public static void CheckInstalledFiles()
		{
			Log.Debug("Looking for installed files...");
			g_p4installed = false;
			g_p4vinstalled = false;
			g_p4customdiff = false;
			g_p4vc_exename = null;
			string p4diff = null;

			// Let's try the default 64 bit installation. Since we are in a 32 bit exe this is tricky
			// to ask the registry...
			string installRoot = null;
			string candidate = @"C:\Program Files\Perforce";
			if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "p4.exe")))
			{
				installRoot = candidate;
			}

			if( null == installRoot )
			{
				installRoot = GetRegistryValue("SOFTWARE\\Perforce\\Environment", "P4INSTROOT", true);

				if (null == installRoot)
				{
					// Perhaps it's an older installation?
					// http://code.google.com/p/niftyplugins/issues/detail?id=47&can=1&q=path
					installRoot = GetRegistryValue("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths", "p4.exe", true);
				}
			}

			if(null != installRoot)
			{
				Log.Info("Found perforce installation at {0}", installRoot);

				g_p4installed = File.Exists(Path.Combine(installRoot, "p4.exe"));
				g_p4vinstalled = File.Exists(Path.Combine(installRoot, "p4v.exe"));
				LookupP4VC((candidateName) => File.Exists(Path.Combine(installRoot, candidateName)));

				Log.Info("[{0}] p4.exe", g_p4installed ? "X" : " ");
				Log.Info("[{0}] p4v.exe", g_p4vinstalled ? "X" : " ");
				Log.Info("[{0}] {1}", g_p4vc_exename != null ? "X" : " ", g_p4vc_exename ?? "p4vc(.bat|.exe)");

				p4diff = GetRegistryValue("SOFTWARE\\Perforce\\Environment", "P4DIFF", true);
				if(!string.IsNullOrEmpty(p4diff))
				{
					Log.Info("[X] p4 custom diff '{0}' from HKLM", p4diff);
					g_p4customdiff = true;
				}
				p4diff = GetRegistryValue("SOFTWARE\\Perforce\\Environment", "P4DIFF", false);
				if(!string.IsNullOrEmpty(p4diff))
				{
					Log.Info("[X] p4 custom diff '{0}' from HKCU", p4diff);
					g_p4customdiff = true;
				}
				p4diff = Environment.GetEnvironmentVariable("P4DIFF");
				if(null != p4diff)
				{
					Log.Info("[X] p4 custom diff '{0}' from P4DIFF env var", p4diff);
					g_p4customdiff = true;
				}

				if (!g_p4customdiff)
					Log.Info("[ ] p4 custom diff");
			}
			else
			{
				// Let's try to find the executables through the path variable instead.
				if(null != Help.FindFileInPath("p4.exe"))
				{
					g_p4installed = true;
					Log.Info("Found p4 in path");
				}

				if(null != Help.FindFileInPath("p4v.exe"))
				{
					g_p4vinstalled = true;
					Log.Info("Found p4v in path");
				}

				if(LookupP4VC((candidateName) => Help.FindFileInPath(candidateName) != null))
				{
					Log.Info("Found {0} in path", g_p4vc_exename);
				}

				Log.Warning("Could not find any peforce installation in the registry!!!");

				p4diff = Environment.GetEnvironmentVariable("P4DIFF");
				if(null != p4diff)
				{
					Log.Info("Found p4 custom diff");
					g_p4customdiff = true;
				}
			}

			DetermineSupportedFeatures();
		}

		private static void DetermineSupportedFeatures()
		{
			// history was added in p4v 2019.2 update1/1883366
			g_p4vc_history_supported = P4VCHasCommand("history");
			Log.Info("[{0}] p4vc history", g_p4vc_history_supported ? "X" : " ");

			// diffhave was added in p4v 2020.1/1946989
			g_p4vc_diffhave_supported = P4VCHasCommand("diffhave");
			Log.Info("[{0}] p4vc diffhave", g_p4vc_diffhave_supported ? "X" : " ");
		}

		private static bool P4VCHasCommand(string command)
		{
			if (string.IsNullOrEmpty(g_p4vc_exename))
				return false;

			string result = Aurora.Process.Execute(g_p4vc_exename, "", $"help {command}", throwIfNonZeroExitCode: false);

			return result.IndexOf("Invalid help command request...", StringComparison.Ordinal) == -1;

		}

		private static bool NotifyUser(string message)
		{
			if(!g_alreadyNotified.Contains(message))
			{
				System.Windows.Forms.MessageBox.Show(message, "NiftyPerforce Notice!", System.Windows.Forms.MessageBoxButtons.OK);
				g_alreadyNotified.Add(message);
			}
			return false;
		}

		public static string RemapToMain(string filename, string mainline)
		{
			Log.Debug("RemapToMain : {0} {1}", filename, mainline);

			if (mainline.Length == 0)
			{
				Log.Error( "Tried to find the mainline version of {0}, but the mainline path spec is empty", filename);
				throw new Exception( string.Format("Tried to find the mainline version of {0}, but the mainline path spec is empty", filename) );
			}

			string result = Aurora.Process.Execute("p4.exe", Path.GetDirectoryName(filename), GetUserInfoString() + "integrated \"" + filename + "\"");

			Regex pattern = new Regex(@"//(.*)#\d+ - .*//([^#]+)#\d+", RegexOptions.Compiled);

			string mainline_ = mainline.ToLowerInvariant();

			foreach( Match m in pattern.Matches(result) )
			{
				string candidate = "//" + m.Groups[2].ToString().ToLowerInvariant();

				if( candidate.StartsWith(mainline_, StringComparison.Ordinal) )
					return candidate;
			}

			return filename;
		}
	}
}
