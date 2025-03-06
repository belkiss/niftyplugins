// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.
using Aurora;
using EnvDTE;

namespace NiftyPerforce.EventHandlers
{
    // Handles registration and events for add/delete files and projects.
    internal sealed class AutoAddDelete : Feature
    {
        private readonly ProjectItemsEvents _projectEvents;
        private readonly SolutionEvents _solutionEvents;
        private readonly Plugin _plugin;

        private _dispProjectItemsEvents_ItemAddedEventHandler? _itemAddedEventHandler;
        private _dispSolutionEvents_ProjectAddedEventHandler? _projectAddedEventHandler;
        private _dispProjectItemsEvents_ItemRemovedEventHandler? _itemRemovedEventHandler;
        private _dispSolutionEvents_ProjectRemovedEventHandler? _projectRemovedEventHandler;

        public AutoAddDelete(Plugin plugin)
            : base("AutoAddDelete")
        {
            _plugin = plugin;

            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            _projectEvents = ((EnvDTE80.Events2)_plugin.App.Events).ProjectItemsEvents;
            _solutionEvents = ((EnvDTE80.Events2)_plugin.App.Events).SolutionEvents;

            ((OptionsDialogPage)_plugin.Options).OnApplyEvent += (s, e) => RegisterEvents();
            RegisterEvents();
        }

        private bool AddFilesHandlersInstalled => _itemAddedEventHandler != null || _projectAddedEventHandler != null;  // second conditional is useless but kept for clarity

        private bool RemoveFilesHandlersInstalled => _itemRemovedEventHandler != null || _projectRemovedEventHandler != null;  // second conditional is useless but kept for clarity

        private void RegisterEvents()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (((OptionsDialogPage)_plugin.Options).AutoAdd)
            {
                if (!AddFilesHandlersInstalled)
                {
                    Log.Info("Adding handlers to automatically add files to perforce as you add them to the project");
                    _itemAddedEventHandler = new _dispProjectItemsEvents_ItemAddedEventHandler(OnItemAdded);
                    _projectEvents.ItemAdded += _itemAddedEventHandler;

                    _projectAddedEventHandler = new _dispSolutionEvents_ProjectAddedEventHandler(OnProjectAdded);
                    _solutionEvents.ProjectAdded += _projectAddedEventHandler;
                }
            }
            else if (AddFilesHandlersInstalled)
            {
                Log.Info("Removing handlers to automatically add files to perforce as you add them to the project");
                _projectEvents.ItemAdded -= _itemAddedEventHandler;
                _itemAddedEventHandler = null;

                _solutionEvents.ProjectAdded -= _projectAddedEventHandler;
                _projectAddedEventHandler = null;
            }

            if (((OptionsDialogPage)_plugin.Options).AutoDelete)
            {
                if (!RemoveFilesHandlersInstalled)
                {
                    Log.Info("Adding handlers to automatically delete files from perforce as you remove them from the project");
                    _itemRemovedEventHandler = new _dispProjectItemsEvents_ItemRemovedEventHandler(OnItemRemoved);
                    _projectEvents.ItemRemoved += _itemRemovedEventHandler;

                    _projectRemovedEventHandler = new _dispSolutionEvents_ProjectRemovedEventHandler(OnProjectRemoved);
                    _solutionEvents.ProjectRemoved += _projectRemovedEventHandler;
                }
            }
            else if (RemoveFilesHandlersInstalled)
            {
                Log.Info("Removing handlers to automatically deleting files from perforce as you remove them from the project");
                _projectEvents.ItemRemoved -= _itemRemovedEventHandler;
                _itemRemovedEventHandler = null;

                _solutionEvents.ProjectRemoved -= _projectRemovedEventHandler;
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

            P4Operations.EditFile(_plugin.App.Solution.FullName, false);
            P4Operations.AddFile(project.FullName);

            // TODO: [jt] We should if the operation is not a add new project but rather a add existing project
            //       step through all the project items and add them to perforce. Or maybe we want the user
            //       to do this herself?
        }

        private void OnProjectRemoved(Project project)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            P4Operations.EditFile(_plugin.App.Solution.FullName, false);
            P4Operations.DeleteFile(project.FullName);

            // TODO: [jt] Do we want to automatically delete the items from perforce here?
        }
    }
}
