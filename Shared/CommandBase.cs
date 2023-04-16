// Copyright (C) 2006-2017 Jim Tilander, 2017-2023 Lambert Clara. See the COPYING file in the project root for full license information.

namespace Aurora
{
    public abstract class CommandBase
    {
        public Plugin Plugin { get; }

        public string Name { get; }

        public string CanonicalName { get; }

        public int CommandId { get; }

        protected CommandBase(string name, string canonicalName, Plugin plugin, int commandId)
        {
            Name = name;
            CanonicalName = canonicalName;
            Plugin = plugin;
            CommandId = commandId;
        }

        public abstract bool OnCommand();   // returns if the command was dispatched or not.

        public abstract bool IsEnabled();   // is the command active?
    }
}
