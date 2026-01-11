// Copyright (C) 2006-2017 Jim Tilander, 2017-2026 Lambert Clara. See the COPYING file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace NiftyPerforce.Core.Tests
{
    [TestClass]
    public class P4UtilsTests
    {
        private sealed class P4UtilsBuilder
        {
            private IFileSystem? _fileSystem;
            private IRegistryService? _registryService;

            public P4UtilsBuilder WithFileSystem(IFileSystem fileSystem)
            {
                _fileSystem = fileSystem;
                return this;
            }

            public P4UtilsBuilder WithRegistryService(IRegistryService registryService)
            {
                _registryService = registryService;
                return this;
            }

            public P4Utils Build()
            {
                _fileSystem ??= new MockFileSystem();
                _registryService ??= new MockRegistryService();

                return new P4Utils(_fileSystem, _registryService);
            }
        }

        [TestMethod]
        public void LocateP4InstallPath_NullOrEmptyArgument()
        {
            P4Utils p4Utils = new P4UtilsBuilder().Build();
            Assert.IsNull(p4Utils.LocateP4InstallPath(null));
            Assert.IsNull(p4Utils.LocateP4InstallPath(string.Empty));
        }

        [TestMethod]
        [DataRow(Environment.SpecialFolder.ProgramFiles)]
        [DataRow(Environment.SpecialFolder.ProgramFiles, P4Utils.P4ExeName)]
        [DataRow(Environment.SpecialFolder.ProgramFiles, P4Utils.P4ExeName, P4Utils.P4VExeName, P4Utils.P4VcBatName)]
        [DataRow(Environment.SpecialFolder.ProgramFiles, P4Utils.P4ExeName, P4Utils.P4VExeName, P4Utils.P4VcExeName, P4Utils.P4VcBatName)]
        [DataRow(Environment.SpecialFolder.ProgramFiles, P4Utils.P4ExeName, P4Utils.P4VExeName, P4Utils.P4VcExeName)]
        [DataRow(Environment.SpecialFolder.ProgramFiles, P4Utils.P4ExeName, P4Utils.P4VExeName)]
        [DataRow(Environment.SpecialFolder.ProgramFiles, P4Utils.P4VcBatName)]
        [DataRow(Environment.SpecialFolder.ProgramFiles, P4Utils.P4VcExeName)]
        [DataRow(Environment.SpecialFolder.ProgramFiles, P4Utils.P4VExeName)]
        [DataRow(Environment.SpecialFolder.ProgramFilesX86)]
        [DataRow(Environment.SpecialFolder.ProgramFilesX86, P4Utils.P4ExeName)]
        [DataRow(Environment.SpecialFolder.ProgramFilesX86, P4Utils.P4ExeName, P4Utils.P4VExeName, P4Utils.P4VcBatName)]
        [DataRow(Environment.SpecialFolder.ProgramFilesX86, P4Utils.P4ExeName, P4Utils.P4VExeName, P4Utils.P4VcExeName, P4Utils.P4VcBatName)]
        [DataRow(Environment.SpecialFolder.ProgramFilesX86, P4Utils.P4ExeName, P4Utils.P4VExeName, P4Utils.P4VcExeName)]
        [DataRow(Environment.SpecialFolder.ProgramFilesX86, P4Utils.P4ExeName, P4Utils.P4VExeName)]
        [DataRow(Environment.SpecialFolder.ProgramFilesX86, P4Utils.P4VcBatName)]
        [DataRow(Environment.SpecialFolder.ProgramFilesX86, P4Utils.P4VcExeName)]
        [DataRow(Environment.SpecialFolder.ProgramFilesX86, P4Utils.P4VExeName)]
        public void LocateP4InstallPath_ProgramFiles(Environment.SpecialFolder specialFolder, params string[] files)
        {
            string specialFolderPath = Environment.GetFolderPath(specialFolder);
            Assert.IsGreaterThan(0, specialFolderPath.Length);

            var fileSystem = new MockFileSystem();
            foreach (string file in files)
                fileSystem.AddEmptyFile(Path.Combine(specialFolderPath, "Perforce", file));

            P4Utils p4Utils = new P4UtilsBuilder()
                .WithFileSystem(fileSystem)
                .Build();

            var hashSet = new HashSet<string>(files);

            string? result = p4Utils.LocateP4InstallPath(P4Utils.P4ExeName);
            Assert.IsTrue(hashSet.Contains(P4Utils.P4ExeName) ? result != null : result == null);
            result = p4Utils.LocateP4InstallPath(P4Utils.P4VExeName);
            Assert.IsTrue(hashSet.Contains(P4Utils.P4VExeName) ? result != null : result == null);
            result = p4Utils.LocateP4InstallPath(P4Utils.P4VcExeName);
            Assert.IsTrue(hashSet.Contains(P4Utils.P4VcExeName) ? result != null : result == null);
            result = p4Utils.LocateP4InstallPath(P4Utils.P4VcBatName);
            Assert.IsTrue(hashSet.Contains(P4Utils.P4VcBatName) ? result != null : result == null);
        }

        [TestMethod]
        [DataRow]
        [DataRow(P4Utils.P4ExeName)]
        [DataRow(P4Utils.P4ExeName, P4Utils.P4VExeName, P4Utils.P4VcBatName)]
        [DataRow(P4Utils.P4ExeName, P4Utils.P4VExeName, P4Utils.P4VcExeName, P4Utils.P4VcBatName)]
        [DataRow(P4Utils.P4ExeName, P4Utils.P4VExeName, P4Utils.P4VcExeName)]
        [DataRow(P4Utils.P4ExeName, P4Utils.P4VExeName)]
        [DataRow(P4Utils.P4VcBatName)]
        [DataRow(P4Utils.P4VcExeName)]
        [DataRow(P4Utils.P4VExeName)]
        public void LocateP4InstallPath_RegistryP4INSTROOT(params string[] files)
        {
            string p4InstRootPath = Path.Combine("C:", "Program Files", "Perforce");
            var registryService = new MockRegistryService();
            registryService.SetValue(@"SOFTWARE\Perforce\Environment", "P4INSTROOT", RegistryHive.LocalMachine, p4InstRootPath);

            var fileSystem = new MockFileSystem();
            foreach (string file in files)
                fileSystem.AddEmptyFile(Path.Combine(p4InstRootPath, file));

            P4Utils p4Utils = new P4UtilsBuilder()
                .WithFileSystem(fileSystem)
                .WithRegistryService(registryService)
                .Build();

            var hashSet = new HashSet<string>(files);

            string? result = p4Utils.LocateP4InstallPath(P4Utils.P4ExeName);
            Assert.IsTrue(hashSet.Contains(P4Utils.P4ExeName) ? result != null : result == null);
            result = p4Utils.LocateP4InstallPath(P4Utils.P4VExeName);
            Assert.IsTrue(hashSet.Contains(P4Utils.P4VExeName) ? result != null : result == null);
            result = p4Utils.LocateP4InstallPath(P4Utils.P4VcExeName);
            Assert.IsTrue(hashSet.Contains(P4Utils.P4VcExeName) ? result != null : result == null);
            result = p4Utils.LocateP4InstallPath(P4Utils.P4VcBatName);
            Assert.IsTrue(hashSet.Contains(P4Utils.P4VcBatName) ? result != null : result == null);
        }

        [TestMethod]
        [DataRow]
        [DataRow(P4Utils.P4ExeName)]
        [DataRow(P4Utils.P4ExeName, P4Utils.P4VExeName, P4Utils.P4VcBatName)]
        [DataRow(P4Utils.P4ExeName, P4Utils.P4VExeName, P4Utils.P4VcExeName, P4Utils.P4VcBatName)]
        [DataRow(P4Utils.P4ExeName, P4Utils.P4VExeName, P4Utils.P4VcExeName)]
        [DataRow(P4Utils.P4ExeName, P4Utils.P4VExeName)]
        [DataRow(P4Utils.P4VcBatName)]
        [DataRow(P4Utils.P4VcExeName)]
        [DataRow(P4Utils.P4VExeName)]
        public void LocateP4InstallPath_RegistryAppPaths(params string[] files)
        {
            var registryService = new MockRegistryService();
            var fileSystem = new MockFileSystem();
            string p4InstRootPath = Path.Combine("C:", "Program Files", "Perforce");
            foreach (string file in files)
            {
                string path = Path.Combine(p4InstRootPath, file);
                registryService.SetValue($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{file}", string.Empty, RegistryHive.LocalMachine, path);
                fileSystem.AddEmptyFile(path);
            }

            P4Utils p4Utils = new P4UtilsBuilder()
                .WithFileSystem(fileSystem)
                .WithRegistryService(registryService)
                .Build();

            var hashSet = new HashSet<string>(files);

            string? result = p4Utils.LocateP4InstallPath(P4Utils.P4ExeName);
            Assert.IsTrue(hashSet.Contains(P4Utils.P4ExeName) ? result != null : result == null);
            result = p4Utils.LocateP4InstallPath(P4Utils.P4VExeName);
            Assert.IsTrue(hashSet.Contains(P4Utils.P4VExeName) ? result != null : result == null);
            result = p4Utils.LocateP4InstallPath(P4Utils.P4VcExeName);
            Assert.IsTrue(hashSet.Contains(P4Utils.P4VcExeName) ? result != null : result == null);
            result = p4Utils.LocateP4InstallPath(P4Utils.P4VcBatName);
            Assert.IsTrue(hashSet.Contains(P4Utils.P4VcBatName) ? result != null : result == null);
        }
    }
}
