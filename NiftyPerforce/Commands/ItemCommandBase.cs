// Copyright (C) 2006-2017 Jim Tilander, 2017-2023 Lambert Clara. See the COPYING file in the project root for full license information.
using System.IO;
using Aurora;
using EnvDTE;

namespace NiftyPerforce
{
    // an item command is a command associated with selected items in solution explorer
    internal abstract class ItemCommandBase : CommandBase
    {
        private const string FileItemGUID = "{6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C}";

        private readonly bool _executeForFileItems = true;
        private readonly bool _executeForProjectItems = true;

        protected ItemCommandBase(string name, string canonicalName, Plugin plugin, bool executeForFileItems, bool executeForProjectItems, int commandId)
            : base(name, canonicalName, plugin, commandId)
        {
            _executeForFileItems = executeForFileItems;
            _executeForProjectItems = executeForProjectItems;
        }

        public override bool OnCommand()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            foreach (SelectedItem sel in Plugin.App.SelectedItems)
            {
                if (_executeForFileItems && Plugin.App.ActiveWindow.Type == vsWindowType.vsWindowTypeDocument)
                {
                    OnExecute(sel, Plugin.App.ActiveDocument.FullName);
                }
                else if (_executeForFileItems && sel.ProjectItem != null && sel.ProjectItem.Kind.ToUpperInvariant() == FileItemGUID)
                {
                    OnExecute(sel, sel.ProjectItem.get_FileNames(0));
                }
                else if (_executeForProjectItems && sel.Project != null)
                {
                    OnExecute(sel, sel.Project.FullName);
                }
            }

            return true;
        }

        public override bool IsEnabled()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            return Plugin.App.SelectedItems.Count > 0;
        }

        protected static bool TryGetDirectoryName(string fileName, out string? dirName)
        {
            dirName = Path.GetDirectoryName(fileName);
            if (dirName == null)
            {
                Log.Error("Couldn't get directory name from '{0}'", fileName);
                return false;
            }

            return true;
        }

        public abstract void OnExecute(SelectedItem item, string fileName);
    }
}
