// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using System;
using System.Collections.Generic;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace Aurora
{
	// Wrapper class around registering other classes to handle the actual commands.
	// Interfaces with visual studio and handles the dispatch.
	public class Plugin
	{
		private OutputWindowPane m_outputPane;
		private readonly string m_panelName;

		private readonly Dictionary<string, Feature> m_features = new Dictionary<string, Feature>();

		public OutputWindowPane OutputPane
		{
			get
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				return GetOutputPane();
			}
		}
		public string Prefix { get; }
		public DTE2 App { get; }
		public Commands Commands { get { return App.Commands; }}
		public OleMenuCommandService MenuCommandService { get; }
		public object Options { get; }

		public Plugin(DTE2 application, OleMenuCommandService oleMenuCommandService, string panelName, string connectPath, object options)
		{
			// TODO: This can be figured out from traversing the assembly and locating the Connect class...
			Prefix = connectPath;

			App = application;
			m_panelName = panelName;
			MenuCommandService = oleMenuCommandService;
			Options = options;
		}

		public void AddFeature(Feature feature)
		{
			m_features.Add(feature.Name, feature);
		}

		public CommandEvents FindCommandEvents(string commandName)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			CommandEvents events = null;
			try
			{
				Command command = App.DTE.Commands.Item(commandName, -1);
				if(command != null)
					events = App.DTE.Events.get_CommandEvents(command.Guid, command.ID);
			}
			catch
			{
			}
			return events;
		}

		private OutputWindowPane GetOutputPane()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (m_outputPane != null)
				return m_outputPane;
			try
			{
				m_outputPane = AquireOutputPane(App, m_panelName);
			}
			catch (Exception)
			{
			}
			return m_outputPane;
		}

		private static OutputWindowPane AquireOutputPane(DTE2 app, string name)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if(string.IsNullOrEmpty(name) || app.Windows.Count == 0)
				return null;

			OutputWindowPane result = FindOutputPane(app, name);
			if(null != result)
				return result;

			OutputWindow outputWindow = (OutputWindow)app.Windows.Item(EnvDTE.Constants.vsWindowKindOutput).Object;
			OutputWindowPanes panes = outputWindow.OutputWindowPanes;
			return panes.Add(name);
		}

		public static OutputWindowPane FindOutputPane(DTE2 app, string name)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if(string.IsNullOrEmpty(name) ||app?.Windows?.Count == null || app.Windows.Count == 0)
				return null;

			OutputWindow outputWindow = (OutputWindow)app.Windows.Item(EnvDTE.Constants.vsWindowKindOutput).Object;
			OutputWindowPanes panes = outputWindow.OutputWindowPanes;

			foreach(OutputWindowPane pane in panes)
			{
				if(name != pane.Name)
					continue;

				return pane;
			}

			return null;
		}
	}
}
