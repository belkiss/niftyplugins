// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.

using System;
using System.Collections.Generic;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace NiftyPerforce
{
    // Wrapper class around registering other classes to handle the actual commands.
    // Interfaces with visual studio and handles the dispatch.
    public class Plugin
    {
        private readonly List<Feature> _features = new List<Feature>();

        public DTE2 App { get; }

        public OleMenuCommandService MenuCommandService { get; }

        public object Options { get; }

        public P4Operations P4Operations { get; }

        public Plugin(DTE2 application, OleMenuCommandService oleMenuCommandService, object options, P4Operations operations)
        {
            App = application;
            MenuCommandService = oleMenuCommandService;
            Options = options;
            P4Operations = operations;
        }

        public void AddFeature(Feature feature)
        {
            _features.Add(feature);
        }

        public CommandEvents? FindCommandEvents(string commandName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            CommandEvents? events = null;
            try
            {
                Command command = App.DTE.Commands.Item(commandName);
                if (command != null)
                    events = App.DTE.Events.CommandEvents[command.Guid, command.ID];
            }
            catch
            {
            }

            return events;
        }
    }
}
