// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
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
        private readonly Dictionary<string, Feature> m_features = new Dictionary<string, Feature>();

        public DTE2 App { get; }
        public Commands Commands => App.Commands;
        public OleMenuCommandService MenuCommandService { get; }
        public object Options { get; }

        public Plugin(DTE2 application, OleMenuCommandService oleMenuCommandService, object options)
        {
            App = application;
            MenuCommandService = oleMenuCommandService;
            Options = options;
        }

        public void AddFeature(Feature feature)
        {
            m_features.Add(feature.Name, feature);
        }

        public CommandEvents? FindCommandEvents(string commandName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            CommandEvents? events = null;
            try
            {
                Command command = App.DTE.Commands.Item(commandName, -1);
                if (command != null)
                    events = App.DTE.Events.get_CommandEvents(command.Guid, command.ID);
            }
            catch
            {
            }

            return events;
        }
    }
}
