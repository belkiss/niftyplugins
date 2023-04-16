// Copyright (C) 2006-2017 Jim Tilander, 2017-2023 Lambert Clara. See the COPYING file in the project root for full license information.
using Aurora;
using EnvDTE;

namespace NiftyPerforce
{
    // Handles registration and events for add/delete files and projects.
    internal sealed class AutoAddDelete : Feature
    {
        private readonly ProjectItemsEvents m_projectEvents;
        private readonly SolutionEvents m_solutionEvents;
        private readonly Plugin m_plugin;

        private _dispProjectItemsEvents_ItemAddedEventHandler? _itemAddedEventHandler;
        private _dispSolutionEvents_ProjectAddedEventHandler? _projectAddedEventHandler;
        private _dispProjectItemsEvents_ItemRemovedEventHandler? _itemRemovedEventHandler;
        private _dispSolutionEvents_ProjectRemovedEventHandler? _projectRemovedEventHandler;

        public AutoAddDelete(Plugin plugin)
            : base("AutoAddDelete")
        {
            m_plugin = plugin;

            m_projectEvents = ((EnvDTE80.Events2)m_plugin.App.Events).ProjectItemsEvents;
            m_solutionEvents = ((EnvDTE80.Events2)m_plugin.App.Events).SolutionEvents;

            ((OptionsDialogPage)m_plugin.Options).OnApplyEvent += (s, e) => RegisterEvents();
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            RegisterEvents();
        }

        private bool AddFilesHandlersInstalled => _itemAddedEventHandler != null || _projectAddedEventHandler != null;  // second conditional is useless but kept for clarity

        private bool RemoveFilesHandlersInstalled => _itemRemovedEventHandler != null || _projectRemovedEventHandler != null;  // second conditional is useless but kept for clarity

        private void RegisterEvents()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (((OptionsDialogPage)m_plugin.Options).AutoAdd)
            {
                if (!AddFilesHandlersInstalled)
                {
                    Log.Info("Adding handlers to automatically add files to perforce as you add them to the project");
                    _itemAddedEventHandler = new _dispProjectItemsEvents_ItemAddedEventHandler(OnItemAdded);
                    m_projectEvents.ItemAdded += _itemAddedEventHandler;

                    _projectAddedEventHandler = new _dispSolutionEvents_ProjectAddedEventHandler(OnProjectAdded);
                    m_solutionEvents.ProjectAdded += _projectAddedEventHandler;
                }
            }
            else if (AddFilesHandlersInstalled)
            {
                Log.Info("Removing handlers to automatically add files to perforce as you add them to the project");
                m_projectEvents.ItemAdded -= _itemAddedEventHandler;
                _itemAddedEventHandler = null;

                m_solutionEvents.ProjectAdded -= _projectAddedEventHandler;
                _projectAddedEventHandler = null;
            }

            if (((OptionsDialogPage)m_plugin.Options).AutoDelete)
            {
                if (!RemoveFilesHandlersInstalled)
                {
                    Log.Info("Adding handlers to automatically delete files from perforce as you remove them from the project");
                    _itemRemovedEventHandler = new _dispProjectItemsEvents_ItemRemovedEventHandler(OnItemRemoved);
                    m_projectEvents.ItemRemoved += _itemRemovedEventHandler;

                    _projectRemovedEventHandler = new _dispSolutionEvents_ProjectRemovedEventHandler(OnProjectRemoved);
                    m_solutionEvents.ProjectRemoved += _projectRemovedEventHandler;
                }
            }
            else if (RemoveFilesHandlersInstalled)
            {
                Log.Info("Removing handlers to automatically deleting files from perforce as you remove them from the project");
                m_projectEvents.ItemRemoved -= _itemRemovedEventHandler;
                _itemRemovedEventHandler = null;

                m_solutionEvents.ProjectRemoved -= _projectRemovedEventHandler;
                _projectRemovedEventHandler = null;
            }
        }

        private void OnItemAdded(ProjectItem item)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            P4Operations.EditFile(item.ContainingProject.FullName, false);

            if (item.ProjectItems != null)
            {
                for (int i = 0; i < item.FileCount; i++)
                {
                    string name = item.get_FileNames((short)i);
                    P4Operations.AddFile(name);
                }
            }
            else
            {
                if (System.IO.File.Exists(item.Name))
                    P4Operations.AddFile(item.Name);
            }
        }

        private void OnItemRemoved(ProjectItem item)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            P4Operations.EditFile(item.ContainingProject.FullName, false);

            for (int i = 0; i < item.FileCount; i++)
            {
                string name = item.get_FileNames((short)i);
                P4Operations.DeleteFile(name);
            }
        }

        private void OnProjectAdded(Project project)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            P4Operations.EditFile(m_plugin.App.Solution.FullName, false);
            P4Operations.AddFile(project.FullName);

            // TODO: [jt] We should if the operation is not a add new project but rather a add existing project
            //       step through all the project items and add them to perforce. Or maybe we want the user
            //       to do this herself?
        }

        private void OnProjectRemoved(Project project)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            P4Operations.EditFile(m_plugin.App.Solution.FullName, false);
            P4Operations.DeleteFile(project.FullName);

            // TODO: [jt] Do we want to automatically delete the items from perforce here?
        }
    }
}
