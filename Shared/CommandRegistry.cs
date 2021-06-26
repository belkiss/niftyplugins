// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
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
        private readonly Dictionary<string, CommandBase> mCommands;
        private readonly Dictionary<int, CommandBase> mCommandsById;
        private readonly Plugin mPlugin;
        private Guid mCmdGroupGuid;

        public CommandRegistry(Plugin plugin, Guid cmdGroupGuid)
        {
            mCommands = new Dictionary<string, CommandBase>();
            mCommandsById = new Dictionary<int, CommandBase>();
            mPlugin = plugin;
            mCmdGroupGuid = cmdGroupGuid;
        }

        public void RegisterCommand(CommandBase commandHandler)
        {
            var command = RegisterCommandPrivate(commandHandler);
            if (command != null)
                mCommands.Add(commandHandler.CanonicalName, commandHandler);
        }

        private OleMenuCommand RegisterCommandPrivate(CommandBase commandHandler)
        {
            OleMenuCommand vscommand = null;
            //if (cmdId == 0)
            {
                OleMenuCommandService menuCommandService = mPlugin.MenuCommandService;
                var commandID = new CommandID(mCmdGroupGuid, commandHandler.CommandId);

                vscommand = new OleMenuCommand(OleMenuCommandCallback, commandID);
                vscommand.BeforeQueryStatus += OleMenuCommandBeforeQueryStatus; // LCTODO: this spams too much, figure out what's wrong
                menuCommandService.AddCommand(vscommand);
                mCommandsById[commandID.ID] = commandHandler;
            }
            // Register the graphics controls for this command as well.
            // First let the command itself have a stab at register whatever it needs.
            // Then by default we always register ourselves in the main toolbar of the application.
            //commandHandler.RegisterGUI(vscommand, mCommandBar, toolbarOnly);

            return vscommand;
        }
        private void OleMenuCommandBeforeQueryStatus(object sender, EventArgs e)
        {

            try
            {
                if (sender is OleMenuCommand oleMenuCommand)
                {
                    CommandID commandId = oleMenuCommand.CommandID;

                    if (commandId != null)
                    {
                        if (mCommandsById.ContainsKey(commandId.ID))
                        {
                            var bc = mCommandsById[commandId.ID];

                            oleMenuCommand.Supported = true;
                            oleMenuCommand.Enabled = mCommandsById[commandId.ID].IsEnabled();
                            oleMenuCommand.Visible = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }

        private void OleMenuCommandCallback(object sender, EventArgs e)
        {
            try
            {
                if (sender is OleMenuCommand oleMenuCommand)
                {
                    CommandID commandId = oleMenuCommand.CommandID;
                    if (commandId != null)
                    {
                        if (mCommandsById.ContainsKey(commandId.ID))
                        {
                            var command = mCommandsById[commandId.ID];

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
