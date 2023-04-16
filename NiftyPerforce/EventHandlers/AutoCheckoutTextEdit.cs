﻿// Copyright (C) 2006-2017 Jim Tilander, 2017-2023 Lambert Clara. See the COPYING file in the project root for full license information.
using System.Collections.Generic;
using Aurora;
using EnvDTE;
using EnvDTE80;

namespace NiftyPerforce
{
    internal sealed class AutoCheckoutTextEdit : PreCommandFeature
    {
        private EnvDTE80.TextDocumentKeyPressEvents? mTextDocEvents;
        private EnvDTE.TextEditorEvents? mTextEditorEvents;

        public AutoCheckoutTextEdit(Plugin plugin)
            : base(plugin, "AutoCheckoutTextEdit")
        {
            ((OptionsDialogPage)mPlugin.Options).OnApplyEvent += (s, e) => RegisterEvents();
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            RegisterEvents();
        }

        private readonly string[] _commands =
        {
            "Edit.Delete",
            "Edit.DeleteBackwards",
            "Edit.Paste",
        };

        private List<string>? _registeredCommands;
        private _dispTextDocumentKeyPressEvents_BeforeKeyPressEventHandler? _beforeKeyPressEventHandler;
        private _dispTextEditorEvents_LineChangedEventHandler? _lineChangedEventHandler;

        private void RegisterEvents()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (((OptionsDialogPage)mPlugin.Options).AutoCheckoutOnEdit)
            {
                if (_registeredCommands == null)
                {
                    Log.Info("Adding handlers for automatically checking out text files as you edit them");
                    _registeredCommands = new List<string>();
                    var events = (EnvDTE80.Events2)mPlugin.App.Events;
                    mTextDocEvents = events.get_TextDocumentKeyPressEvents(null);
                    _beforeKeyPressEventHandler = new _dispTextDocumentKeyPressEvents_BeforeKeyPressEventHandler(OnBeforeKeyPress);
                    mTextDocEvents.BeforeKeyPress += _beforeKeyPressEventHandler;

                    mTextEditorEvents = events.get_TextEditorEvents(null);
                    _lineChangedEventHandler = new _dispTextEditorEvents_LineChangedEventHandler(OnLineChanged);
                    mTextEditorEvents.LineChanged += _lineChangedEventHandler;

                    foreach (string command in _commands)
                    {
                        if (RegisterHandler(command, OnCheckoutCurrentDocument))
                            _registeredCommands.Add(command);
                        else
                            Log.Warning("Failed to register {0} to command '{1}'", nameof(OnCheckoutCurrentDocument), command);
                    }
                }
            }
            else if (_registeredCommands != null)
            {
                Log.Info("Removing handlers for automatically checking out text files as you edit them");
                foreach (string command in _registeredCommands)
                    UnregisterHandler(command, OnCheckoutCurrentDocument);
                _registeredCommands = null;

                mTextEditorEvents!.LineChanged -= _lineChangedEventHandler;
                mTextEditorEvents = null;

                mTextDocEvents!.BeforeKeyPress -= _beforeKeyPressEventHandler;
                mTextDocEvents = null;
            }
        }

        private void OnBeforeKeyPress(string keypress, EnvDTE.TextSelection selection, bool inStatementCompletion, ref bool cancelKeypress)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (mPlugin.App.ActiveDocument != null && mPlugin.App.ActiveDocument.ReadOnly)
                P4Operations.EditFile(mPlugin.App.ActiveDocument.FullName, false);
        }

        // [jt] This handler checks for things like paste operations. In theory we should be able to remove the handler above, but
        // I can't get this one to fire reliably... Wonder how much these handlers will slow down the IDE?
        private void OnLineChanged(TextPoint startPoint, TextPoint endPoint, int hint)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            if ((hint & (int)vsTextChanged.vsTextChangedNewline) == 0 &&
                (hint & (int)vsTextChanged.vsTextChangedMultiLine) == 0 &&
                (hint & (int)vsTextChanged.vsTextChangedNewline) == 0 &&
                (hint != 0))
                return;
            if (mPlugin.App.ActiveDocument != null && mPlugin.App.ActiveDocument.ReadOnly && !mPlugin.App.ActiveDocument.Saved)
                P4Operations.EditFile(mPlugin.App.ActiveDocument.FullName, false);
        }

        private void OnCheckoutCurrentDocument(string guid, int id, object customIn, object customOut, ref bool cancelDefault)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (mPlugin.App.ActiveDocument != null && mPlugin.App.ActiveDocument.ReadOnly && !mPlugin.App.ActiveDocument.Saved)
                P4Operations.EditFile(mPlugin.App.ActiveDocument.FullName, false);
        }
    }
}
