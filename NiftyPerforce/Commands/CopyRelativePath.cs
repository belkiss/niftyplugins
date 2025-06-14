// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.

using System;
using System.IO;
using System.Windows.Forms;
using EnvDTE;

namespace NiftyPerforce.Commands
{
    internal sealed class CopyRelativePath : ItemCommandBase
    {
        private string? _relativePath;

        public CopyRelativePath(Plugin plugin, string canonicalName)
            : base("CopyRelativePath", canonicalName, plugin, true, true, PackageIds.NiftyCopyRelativePath)
        {
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
                string? pathToCopy = GetRelativePath(GetRelativeToDirectory(), Plugin.App.ActiveDocument?.FullName);
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

        protected override void OnExecute(SelectedItem item, string fileName)
        {
            // do nothing, as we override OnCommand() instead
        }

        private string? SelectBasePath()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            using var folderDialog = new FolderBrowserDialog();

            folderDialog.Description = "Select the base path to compute the relative path (hold shift to ask again next time)";
            if (Plugin.App.ActiveDocument?.FullName != null)
            {
                folderDialog.SelectedPath = Path.GetDirectoryName(Plugin.App.ActiveDocument.FullName);
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
            if (Control.ModifierKeys == Keys.Shift || Plugin.App.Solution?.FullName == null)
            {
                _relativePath = SelectBasePath();
                return _relativePath;
            }

            return Path.GetDirectoryName(Plugin.App.Solution.FullName);
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
