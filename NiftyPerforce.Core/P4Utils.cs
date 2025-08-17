// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.

using System;
using System.IO;
using System.IO.Abstractions;
using Microsoft.Win32;

namespace NiftyPerforce.Core
{
    public class P4Utils
    {
        public const string P4ExeName = "p4.exe";
        public const string P4VExeName = "p4v.exe";

        // starting with 2021.1/2075061
        //    #105247 (Change #2069769)
        //      The p4vc.exe executable has been removed from the Windows installers.
        //      To start P4VC, use the p4vc.bat script.
        public const string P4VcBatName = "p4vc.bat";
        public const string P4VcExeName = "p4vc.exe";

        private readonly IFileSystem _fileSystemService;
        private readonly IRegistryService _registryService;

        public P4Utils(IFileSystem fileSystemService, IRegistryService registryService)
        {
            _fileSystemService = fileSystemService;
            _registryService = registryService;
        }

        public static string EscapeP4Path(string filename)
        {
            return filename.Replace("%", "%25").Replace("#", "%23").Replace("@", "%40");
        }

        public static string UnEscapeP4Path(string escapedfilename)
        {
            return escapedfilename.Replace("%40", "@").Replace("%23", "#").Replace("%25", "%");
        }

        /// <summary>
        /// Finds the Perforce installation path by checking in common installation locations.
        /// </summary>
        /// <param name="fileName">The file to find (p4.exe for instance).</param>
        /// <returns>The full path of the file, or null if not found.</returns>
        public string? LocateP4InstallPath(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            string? candidate;

            // start with program files
            foreach (Environment.SpecialFolder specialFolder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
            {
                string specialFolderPath = Environment.GetFolderPath(specialFolder);
                if (!string.IsNullOrEmpty(specialFolderPath))
                {
                    candidate = _fileSystemService.Path.Combine(specialFolderPath, "Perforce", fileName!);
                    if (_fileSystemService.File.Exists(candidate))
                        return candidate;
                }
            }

            // then registry
            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            {
                // start with P4INSTROOT
                string? installRoot = GetRegistryValue(@"SOFTWARE\Perforce\Environment", "P4INSTROOT", hive);
                if (installRoot != null)
                {
                    candidate = _fileSystemService.Path.Combine(installRoot, fileName!);

                    if (_fileSystemService.File.Exists(candidate))
                        return candidate;
                }

                // then AppPaths
                // cf. https://learn.microsoft.com/en-us/windows/win32/shell/app-registration#using-the-app-paths-subkey
                // Pass string.Empty to get the (Default) entry, which is used as the file's fully qualified path.
                candidate = GetRegistryValue($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{fileName!}", string.Empty, hive);
                if (candidate != null && _fileSystemService.File.Exists(candidate))
                    return candidate;
            }

            // and finally, try to find the executable through the path environment variable
            return FindFileInPath(fileName!);
        }

        private string? FindFileInPath(string fileName)
        {
            string pathEnvVarValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string path in pathEnvVarValue.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrEmpty(path))
                {
                    string candidate = _fileSystemService.Path.Combine(path, fileName);
                    if (_fileSystemService.File.Exists(candidate))
                        return candidate;
                }
            }

            return null;
        }

        private string? GetRegistryValue(string key, string name, RegistryHive hive)
        {
            string? value = _registryService.GetValue(key, name, hive);
            if (!string.IsNullOrEmpty(value))
                return value;

            Log.Debug(@"Could not find registry key {0}\{1}", hive.ToString(), key);
            return null;
        }
    }
}
