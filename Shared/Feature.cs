using EnvDTE;

namespace Aurora
{
    public abstract class Feature
    {
        private readonly string mName;

        public string Name => mName;

        protected Feature(string name)
        {
            mName = name;
        }

        public virtual bool Execute()
        {
            return true;
        }
    };

    public abstract class PreCommandFeature : Feature
    {
        protected Plugin mPlugin;

        protected PreCommandFeature(Plugin plugin, string name)
            : base(name)
        {
            mPlugin = plugin;
        }

        protected bool RegisterHandler(string commandName, _dispCommandEvents_BeforeExecuteEventHandler handler)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            CommandEvents events = mPlugin.FindCommandEvents(commandName);
            if (null == events)
                return false;
            events.BeforeExecute += handler;
            return true;
        }

        protected void UnregisterHandler(string commandName, _dispCommandEvents_BeforeExecuteEventHandler handler)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            CommandEvents events = mPlugin.FindCommandEvents(commandName);
            if (null == events)
                return;
            events.BeforeExecute -= handler;
        }
    };
}
