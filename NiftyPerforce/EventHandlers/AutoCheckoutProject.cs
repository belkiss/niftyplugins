// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using System;
using System.Collections.Generic;
using EnvDTE;
using NiftyPerforce;

namespace Aurora
{
	namespace NiftyPerforce
	{
		class AutoCheckoutProject : PreCommandFeature
		{
			public AutoCheckoutProject(Plugin plugin)
				: base(plugin, "AutoCheckoutProject", "Automatically checks out the project files")
			{
				((Config)mPlugin.Options).OnApplyEvent += RegisterEvents;
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
			private List<string> _registeredCommands;

			private void RegisterEvents(object sender = null, EventArgs e = null)
			{
				if (((Config)mPlugin.Options).AutoCheckoutProject)
				{
					if (_registeredCommands == null)
					{
						Log.Info("Adding handlers for automatically checking out .vcproj files when you do changes to the project");
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
				if (Guid == "{5EFC7975-14BC-11CF-9B2B-00AA00573819}" && ID == 17)
				{
					// see if the active window is SolutionExplorer :
					Window w = mPlugin.App.ActiveWindow;
					if(w.Type != EnvDTE.vsWindowType.vsWindowTypeSolutionExplorer)
					{
						// it's just a delete in the text window, get out !
						return;
					}
				}

				foreach(Project project in (Array)mPlugin.App.ActiveSolutionProjects)
				{
					P4Operations.EditFileImmediate(mPlugin.OutputPane, project.FullName);
				}
			}
		}
	}
}
