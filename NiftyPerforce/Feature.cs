// SPDX-FileCopyrightText: 2006-2017 Jim Tilander
// SPDX-FileCopyrightText: 2017-2026 Lambert Clara
//
// SPDX-License-Identifier: MIT

using EnvDTE;

namespace NiftyPerforce
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
        protected Plugin Plugin { get; }

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
