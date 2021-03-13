// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using EnvDTE;
using NiftyPerforce;
using System;

namespace Aurora
{
	namespace NiftyPerforce
	{
		// Handles registration and events for add/delete files and projects.
		class AutoAddDelete : Feature
		{
			private ProjectItemsEvents m_projectEvents;
			private SolutionEvents m_solutionEvents;

			private _dispProjectItemsEvents_ItemAddedEventHandler _itemAddedEventHandler;
			private _dispSolutionEvents_ProjectAddedEventHandler _projectAddedEventHandler;
			private _dispProjectItemsEvents_ItemRemovedEventHandler _itemRemovedEventHandler;
			private _dispSolutionEvents_ProjectRemovedEventHandler _projectRemovedEventHandler;

			private Plugin m_plugin;

			public AutoAddDelete(Plugin plugin)
				: base("AutoAddDelete", "Automatically adds and deletes files matching project add/delete")
			{
				m_plugin = plugin;

				m_projectEvents = ((EnvDTE80.Events2)m_plugin.App.Events).ProjectItemsEvents;
				m_solutionEvents = ((EnvDTE80.Events2)m_plugin.App.Events).SolutionEvents;

				((Config)m_plugin.Options).OnApplyEvent += RegisterEvents;
				RegisterEvents();
			}

			private bool AddFilesHandlersInstalled { get { return _itemAddedEventHandler != null || _projectAddedEventHandler != null; } } // second conditional is useless but kept for clarity
			private bool RemoveFilesHandlersInstalled { get { return _itemRemovedEventHandler != null || _projectRemovedEventHandler != null; } } // second conditional is useless but kept for clarity

			private void RegisterEvents(object sender = null, EventArgs e = null)
			{
				if (((Config)m_plugin.Options).AutoAdd)
				{
					if (!AddFilesHandlersInstalled)
					{
						Log.Info("Adding handlers for automatically add files to perforce as you add them to the project");
						_itemAddedEventHandler = new _dispProjectItemsEvents_ItemAddedEventHandler(OnItemAdded);
						m_projectEvents.ItemAdded += _itemAddedEventHandler;

						_projectAddedEventHandler = new _dispSolutionEvents_ProjectAddedEventHandler(OnProjectAdded);
						m_solutionEvents.ProjectAdded += _projectAddedEventHandler;
					}
				}
				else if (AddFilesHandlersInstalled)
				{
					Log.Info("Removing handlers for automatically add files to perforce as you add them to the project");
					m_projectEvents.ItemAdded -= _itemAddedEventHandler;
					_itemAddedEventHandler = null;

					m_solutionEvents.ProjectAdded -= _projectAddedEventHandler;
					_projectAddedEventHandler = null;
				}

				if (((Config)m_plugin.Options).AutoDelete)
				{
					if (!RemoveFilesHandlersInstalled)
					{
						Log.Info("Adding handlers for automatically deleting files from perforce as you remove them from the project");
						_itemRemovedEventHandler = new _dispProjectItemsEvents_ItemRemovedEventHandler(OnItemRemoved);
						m_projectEvents.ItemRemoved += _itemRemovedEventHandler;

						_projectRemovedEventHandler = new _dispSolutionEvents_ProjectRemovedEventHandler(OnProjectRemoved);
						m_solutionEvents.ProjectRemoved += _projectRemovedEventHandler;
					}
				}
				else if (RemoveFilesHandlersInstalled)
				{
					Log.Info("Removing handlers for automatically deleting files from perforce as you remove them from the project");
					m_projectEvents.ItemRemoved -= _itemRemovedEventHandler;
					_itemRemovedEventHandler = null;

					m_solutionEvents.ProjectRemoved -= _projectRemovedEventHandler;
					_projectRemovedEventHandler = null;
				}
			}

			public void OnItemAdded(ProjectItem item)
			{
				Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

				P4Operations.EditFile(m_plugin.OutputPane, item.ContainingProject.FullName);

				if (item.ProjectItems != null)
				{
					for (int i = 0; i < item.FileCount; i++)
					{
						string name = item.get_FileNames((short)i);
						P4Operations.AddFile(m_plugin.OutputPane, name);
					}
				}
				else
				{
					if(System.IO.File.Exists(item.Name))
						P4Operations.AddFile(m_plugin.OutputPane, item.Name);
				}
			}

			public void OnItemRemoved(ProjectItem item)
			{
				Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

				P4Operations.EditFile(m_plugin.OutputPane, item.ContainingProject.FullName);

				for (int i = 0; i < item.FileCount; i++)
				{
					string name = item.get_FileNames((short)i);
					P4Operations.DeleteFile(m_plugin.OutputPane, name);
				}
			}

			private void OnProjectAdded(Project project)
			{
				Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

				P4Operations.EditFile(m_plugin.OutputPane, m_plugin.App.Solution.FullName);
				P4Operations.AddFile(m_plugin.OutputPane, project.FullName);
				// TODO: [jt] We should if the operation is not a add new project but rather a add existing project
				//       step through all the project items and add them to perforce. Or maybe we want the user
				//       to do this herself?
			}

			private void OnProjectRemoved(Project project)
			{
				Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

				P4Operations.EditFile(m_plugin.OutputPane, m_plugin.App.Solution.FullName);
				P4Operations.DeleteFile(m_plugin.OutputPane, project.FullName);
				// TODO: [jt] Do we want to automatically delete the items from perforce here?
			}
		}
	}
}
