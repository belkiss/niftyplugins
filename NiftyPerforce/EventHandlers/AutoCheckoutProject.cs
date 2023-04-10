// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using System;
using System.Collections.Generic;
using Aurora;
using EnvDTE;

namespace NiftyPerforce
{
    internal class AutoCheckoutProject : PreCommandFeature
    {
        public AutoCheckoutProject(Plugin plugin)
            : base(plugin, "AutoCheckoutProject")
        {
            ((Config)mPlugin.Options).OnApplyEvent += (s, e) => RegisterEvents();
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
                "File.Remove" // I don't think this actually does anything
            };

        private List<string>? _registeredCommands;

        private void RegisterEvents()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (((Config)mPlugin.Options).AutoCheckoutProject)
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

        private void OnCheckoutSelectedProjects(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            // when I get Edit.Delete :
            if (Guid == Microsoft.VisualStudio.VSConstants.CMDSETID.StandardCommandSet97_string && ID == 17)
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
