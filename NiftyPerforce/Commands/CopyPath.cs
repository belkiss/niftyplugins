// Copyright (C) 2006-2017 Jim Tilander, 2017-2026 Lambert Clara. See the COPYING file in the project root for full license information.

using System;
using System.IO;
using System.Windows.Forms;
using EnvDTE;

namespace NiftyPerforce.Commands
{
    internal sealed class CopyPath : ItemCommandBase
    {
        private readonly Mode _mode;
        private string? _relativePath;

        internal enum Mode
        {
            /// <summary>
            /// Copy the current file path relative to the solution directory.
            /// </summary>
            RelativePath,

            /// <summary>
            /// Copy the current file name only.
            /// </summary>
            FileName,
        }

        public CopyPath(Plugin plugin, string canonicalName, Mode mode)
            : base("CopyPath", canonicalName, plugin, true, true, mode == Mode.FileName ? PackageIds.NiftyCopyFileName : PackageIds.NiftyCopyRelativePath)
        {
            _mode = mode;
        }

        public override bool IsEnabled()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            return !string.IsNullOrWhiteSpace(Plugin.App.ActiveDocument?.FullName);
        }

        public override bool OnCommand()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                string? activeDoc = Plugin.App.ActiveDocument?.FullName;
                string? pathToCopy = _mode == Mode.FileName ? Path.GetFileName(activeDoc) : GetRelativePath(GetRelativeToDirectory(), activeDoc);
                if (!string.IsNullOrWhiteSpace(pathToCopy))
                {
                    Clipboard.SetText(pathToCopy);
                }
            }
            catch
            {
            }

            return true;
        }

        protected override void OnExecute(SelectedItem item, string filePath)
        {
            // do nothing, as we override OnCommand() instead
        }

        private string? SelectBasePath()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            using var folderDialog = new FolderBrowserDialog();

            folderDialog.Description = "Select the base path to compute the relative path (hold shift to ask again next time)";
            string? activeDoc = Plugin.App.ActiveDocument?.FullName;
            if (!string.IsNullOrEmpty(activeDoc))
            {
                folderDialog.SelectedPath = Path.GetDirectoryName(activeDoc);
            }

            return folderDialog.ShowDialog() == DialogResult.OK ? folderDialog.SelectedPath : null;
        }

        private string? GetRelativeToDirectory()
        {
            if (Control.ModifierKeys != Keys.Shift && !string.IsNullOrEmpty(_relativePath))
            {
                return _relativePath;
            }

            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            string? solutionPath = Plugin.App.Solution?.FullName;
            if (Control.ModifierKeys == Keys.Shift || string.IsNullOrWhiteSpace(solutionPath))
            {
                _relativePath = SelectBasePath();
                return _relativePath;
            }

            return Path.GetDirectoryName(solutionPath);
        }

        private static string? GetRelativePath(string? relativeTo, string? filePath)
        {
            if (filePath == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(relativeTo))
            {
                return filePath;
            }

            // Calculate the relative path
            var solutionUri = new Uri(relativeTo + Path.DirectorySeparatorChar);
            var fileUri = new Uri(Path.GetFullPath(filePath));

            Uri relativeUri = solutionUri.MakeRelativeUri(fileUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);

            return relativePath;
        }
    }
}
