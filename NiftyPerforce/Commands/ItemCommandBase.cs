// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using EnvDTE;
using Aurora;

namespace NiftyPerforce
{
    // an item command is a command associated with selected items in solution explorer
    abstract class ItemCommandBase : CommandBase
    {
        private bool m_executeForFileItems = true;
        private bool m_executeForProjectItems = true;

        protected ItemCommandBase(string name, string canonicalName, Plugin plugin, bool executeForFileItems, bool executeForProjectItems, int commandId)
            : base(name, canonicalName, plugin, commandId)
        {
            m_executeForFileItems = executeForFileItems;
            m_executeForProjectItems = executeForProjectItems;
        }

        private const string m_fileItemGUID = "{6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C}";

        public override bool OnCommand()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            foreach (SelectedItem sel in Plugin.App.SelectedItems)
            {
                if (m_executeForFileItems && Plugin.App.ActiveWindow.Type == vsWindowType.vsWindowTypeDocument)
                {
                    OnExecute(sel, Plugin.App.ActiveDocument.FullName, Plugin.OutputPane);
                }
                else if (m_executeForFileItems && sel.ProjectItem != null && m_fileItemGUID == sel.ProjectItem.Kind.ToUpperInvariant())
                {
                    OnExecute(sel, sel.ProjectItem.get_FileNames(0), Plugin.OutputPane);
                }
                else if (m_executeForProjectItems && sel.Project != null)
                {
                    OnExecute(sel, sel.Project.FullName, Plugin.OutputPane);
                }
            }

            return true;
        }

        public override bool IsEnabled()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            return Plugin.App.SelectedItems.Count > 0;
        }

        public abstract void OnExecute(SelectedItem item, string fileName, OutputWindowPane pane);
    }
}
