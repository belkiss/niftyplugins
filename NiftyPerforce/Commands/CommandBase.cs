// SPDX-FileCopyrightText: 2006-2017 Jim Tilander
// SPDX-FileCopyrightText: 2017-2026 Lambert Clara
//
// SPDX-License-Identifier: MIT

namespace NiftyPerforce.Commands
{
    public abstract class CommandBase
    {
        protected Plugin Plugin { get; }

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
