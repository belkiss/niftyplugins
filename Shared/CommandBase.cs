// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.

namespace Aurora
{

	public abstract class CommandBase
	{
		public Plugin Plugin { get; }
		public string Name { get; }
		public string CanonicalName { get; }
		public int CommandId { get; }

		public CommandBase(string name, string canonicalName, Plugin plugin, int commandId)
		{
			Name = name;
			CanonicalName = canonicalName;
			Plugin = plugin;
			CommandId = commandId;
		}

		public abstract bool OnCommand();	// returns if the command was dispatched or not.
		public abstract bool IsEnabled();	// is the command active?
	}
}
