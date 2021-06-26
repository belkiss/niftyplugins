using System;
using System.Collections.Generic;
using System.Text;
using EnvDTE;
using EnvDTE80;

namespace Aurora
{
	public abstract class Feature
	{
		private readonly string mName;
		private readonly string mTooltip;

		public string Name { get { return mName; } }

		protected Feature(string name, string tooltip)
		{
			mName = name;
			mTooltip = tooltip;
		}

		public virtual bool Execute()
		{
			return true;
		}
	};

	public abstract class PreCommandFeature : Feature
	{
		protected Plugin mPlugin;

		protected PreCommandFeature(Plugin plugin, string name, string tooltip)
			: base(name, tooltip)
		{
			mPlugin = plugin;
		}

		protected bool RegisterHandler(string commandName, _dispCommandEvents_BeforeExecuteEventHandler handler)
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
			CommandEvents events = mPlugin.FindCommandEvents(commandName);
			if(null == events)
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
