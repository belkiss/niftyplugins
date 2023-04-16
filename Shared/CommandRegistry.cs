// Copyright (C) 2006-2017 Jim Tilander, 2017-2023 Lambert Clara. See the COPYING file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;

namespace Aurora
{
    // Holds a dictionary between local command names and the instance that holds
    // the logic to execute and update the command itself.
    public class CommandRegistry
    {
        private readonly Dictionary<string, CommandBase> _commands;
        private readonly Dictionary<int, CommandBase> _commandsById;
        private readonly Plugin _plugin;
        private readonly Guid _cmdGroupGuid;

        public CommandRegistry(Plugin plugin, Guid cmdGroupGuid)
        {
            _commands = new Dictionary<string, CommandBase>();
            _commandsById = new Dictionary<int, CommandBase>();
            _plugin = plugin;
            _cmdGroupGuid = cmdGroupGuid;
        }

        public void RegisterCommand(CommandBase commandHandler)
        {
            OleMenuCommand command = RegisterCommandPrivate(commandHandler);
            if (command != null)
                _commands.Add(commandHandler.CanonicalName, commandHandler);
        }

        private OleMenuCommand RegisterCommandPrivate(CommandBase commandHandler)
        {
            OleMenuCommandService menuCommandService = _plugin.MenuCommandService;
            var commandID = new CommandID(_cmdGroupGuid, commandHandler.CommandId);

            var vscommand = new OleMenuCommand(OleMenuCommandCallback, commandID);
            vscommand.BeforeQueryStatus += OleMenuCommandBeforeQueryStatus; // LCTODO: this spams too much, figure out what's wrong
            menuCommandService.AddCommand(vscommand);
            _commandsById[commandID.ID] = commandHandler;

            return vscommand;
        }

        private void OleMenuCommandBeforeQueryStatus(object? sender, EventArgs e)
        {
            try
            {
                if (sender is OleMenuCommand oleMenuCommand)
                {
                    CommandID? commandId = oleMenuCommand.CommandID;

                    if (commandId != null && _commandsById.TryGetValue(commandId.ID, out CommandBase? bc))
                    {
                        oleMenuCommand.Supported = true;
                        oleMenuCommand.Enabled = bc.IsEnabled();
                        oleMenuCommand.Visible = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }

        private void OleMenuCommandCallback(object? sender, EventArgs e)
        {
            try
            {
                if (sender is OleMenuCommand oleMenuCommand)
                {
                    CommandID? commandId = oleMenuCommand.CommandID;
                    if (commandId != null)
                    {
                        if (_commandsById.TryGetValue(commandId.ID, out CommandBase? command))
                        {
                            bool dispatched = command.OnCommand();
                            Log.Debug($"{command.Name} (0x{commandId.ID:X}) " + (dispatched ? "was dispatched" : "fail"));
                        }
                        else
                        {
                            Log.Debug($"Couldn't find command with id 0x{commandId.ID:X}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }
    }
}
