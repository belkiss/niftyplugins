// Copyright (C) 2006-2017 Jim Tilander, 2017-2023 Lambert Clara. See the COPYING file in the project root for full license information.
using System;
using System.Collections.Generic;
using Aurora;
using EnvDTE;

namespace NiftyPerforce
{
    internal sealed class AutoCheckoutProject : PreCommandFeature
    {
        public AutoCheckoutProject(Plugin plugin)
            : base(plugin, "AutoCheckoutProject")
        {
            ((OptionsDialogPage)mPlugin.Options).OnApplyEvent += (s, e) => RegisterEvents();
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            RegisterEvents();
        }

        private readonly string[] _commands =
        {
            "ClassViewContextMenus.ClassViewProject.Properties",
            "ClassViewContextMenus.ClassViewMultiselectProjectreferencesItems.Properties",
            "File.Properties",
            "View.PropertiesWindow",
            "Project.Properties",
            "Project.AddNewItem",
            "Project.AddExistingItem",
            "Edit.Delete", // hmm : removing a file from Solution Explorer is just Edit.Delete !?
            "File.Remove", // I don't think this actually does anything
        };

        private List<string>? _registeredCommands;

        private void RegisterEvents()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (((OptionsDialogPage)mPlugin.Options).AutoCheckoutProject)
            {
                if (_registeredCommands == null)
                {
                    Log.Info("Adding handlers for automatically checking out .vcproj files when you do changes to the project");
                    _registeredCommands = new List<string>();
                    foreach (string command in _commands)
                    {
                        if (RegisterHandler(command, OnCheckoutSelectedProjects))
                            _registeredCommands.Add(command);
                        else
                            Log.Warning("Failed to register {0} to command '{1}'", nameof(OnCheckoutSelectedProjects), command);
                    }
                }
            }
            else if (_registeredCommands != null)
            {
                Log.Info("Removing handlers for automatically checking out .vcproj files when you do changes to the project");
                foreach (string command in _registeredCommands)
                    UnregisterHandler(command, OnCheckoutSelectedProjects);
                _registeredCommands = null;
            }
        }

        private void OnCheckoutSelectedProjects(string guid, int id, object customIn, object customOut, ref bool cancelDefault)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            // when I get Edit.Delete :
            if (guid == Microsoft.VisualStudio.VSConstants.CMDSETID.StandardCommandSet97_string && id == 17)
            {
                // see if the active window is SolutionExplorer :
                Window w = mPlugin.App.ActiveWindow;
                if (w.Type != EnvDTE.vsWindowType.vsWindowTypeSolutionExplorer)
                {
                    // it's just a delete in the text window, get out !
                    return;
                }
            }

            foreach (Project project in (Array)mPlugin.App.ActiveSolutionProjects)
            {
                P4Operations.EditFileImmediate(project.FullName);
            }
        }
    }
}
