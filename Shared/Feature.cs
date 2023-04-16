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
        protected Plugin mPlugin { get; private set; }

        protected PreCommandFeature(Plugin plugin, string name)
            : base(name)
        {
            mPlugin = plugin;
        }

        protected bool RegisterHandler(string commandName, _dispCommandEvents_BeforeExecuteEventHandler handler)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            CommandEvents? events = mPlugin.FindCommandEvents(commandName);
            if (events == null)
                return false;
            events.BeforeExecute += handler;
            return true;
        }

        protected void UnregisterHandler(string commandName, _dispCommandEvents_BeforeExecuteEventHandler handler)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            CommandEvents? events = mPlugin.FindCommandEvents(commandName);
            if (events == null)
                return;
            events.BeforeExecute -= handler;
        }
    }
}
