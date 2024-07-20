// Copyright (C) 2006-2017 Jim Tilander, 2017-2024 Lambert Clara. See the COPYING file in the project root for full license information.
using EnvDTE;

namespace Aurora
{
    public abstract class Feature
    {
        public string Name { get; }

        protected Feature(string name)
        {
            Name = name;
        }
    }

    public abstract class PreCommandFeature : Feature
    {
        protected Plugin Plugin { get; private set; }

        protected PreCommandFeature(Plugin plugin, string name)
            : base(name)
        {
            Plugin = plugin;
        }

        protected bool RegisterHandler(string commandName, _dispCommandEvents_BeforeExecuteEventHandler handler)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            CommandEvents? events = Plugin.FindCommandEvents(commandName);
            if (events == null)
                return false;
            events.BeforeExecute += handler;
            return true;
        }

        protected void UnregisterHandler(string commandName, _dispCommandEvents_BeforeExecuteEventHandler handler)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            CommandEvents? events = Plugin.FindCommandEvents(commandName);
            if (events == null)
                return;
            events.BeforeExecute -= handler;
        }
    }
}
