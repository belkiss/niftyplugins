// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using NiftyPerforce;
using System;
using System.Collections.Generic;

namespace Aurora
{
    namespace NiftyPerforce
    {
        class AutoCheckoutTextEdit : PreCommandFeature
        {
            private EnvDTE80.TextDocumentKeyPressEvents mTextDocEvents;
            private EnvDTE.TextEditorEvents mTextEditorEvents;

            public AutoCheckoutTextEdit(Plugin plugin)
                : base(plugin, "AutoCheckoutTextEdit", "Automatically checks out the text file on edits")
            {
                //((Config)mPlugin.Options).RegisterOnApplyAction(RegisterEvents);
                ((Config)mPlugin.Options).OnApplyEvent += RegisterEvents;
                RegisterEvents();
            }

            private readonly string[] _commands =
            {
                "Edit.Delete",
                "Edit.DeleteBackwards",
                "Edit.Paste"
            };
            private List<string> _registeredCommands;
            private _dispTextDocumentKeyPressEvents_BeforeKeyPressEventHandler _beforeKeyPressEventHandler;
            private _dispTextEditorEvents_LineChangedEventHandler _lineChangedEventHandler;

            private void RegisterEvents(object sender = null, EventArgs e = null)
            {
                if (((Config)mPlugin.Options).AutoCheckoutOnEdit)
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

                    mTextEditorEvents.LineChanged -= _lineChangedEventHandler;
                    mTextEditorEvents = null;

                    mTextDocEvents.BeforeKeyPress -= _beforeKeyPressEventHandler;
                    mTextDocEvents = null;
                }
            }

            private void OnBeforeKeyPress(string Keypress, EnvDTE.TextSelection Selection, bool InStatementCompletion, ref bool CancelKeypress)
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

                if (mPlugin.App.ActiveDocument != null && mPlugin.App.ActiveDocument.ReadOnly)
                    P4Operations.EditFile(mPlugin.OutputPane, mPlugin.App.ActiveDocument.FullName);
            }

            // [jt] This handler checks for things like paste operations. In theory we should be able to remove the handler above, but
            // I can't get this one to fire reliably... Wonder how much these handlers will slow down the IDE?
            private void OnLineChanged(TextPoint StartPoint, TextPoint EndPoint, int Hint)
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                if ((Hint & (int)vsTextChanged.vsTextChangedNewline) == 0 &&
                    (Hint & (int)vsTextChanged.vsTextChangedMultiLine) == 0 &&
                    (Hint & (int)vsTextChanged.vsTextChangedNewline) == 0 &&
                    (Hint != 0))
                    return;
                if (mPlugin.App.ActiveDocument != null && mPlugin.App.ActiveDocument.ReadOnly && !mPlugin.App.ActiveDocument.Saved)
                    P4Operations.EditFile(mPlugin.OutputPane, mPlugin.App.ActiveDocument.FullName);
            }

            private void OnCheckoutCurrentDocument(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

                if (mPlugin.App.ActiveDocument != null && mPlugin.App.ActiveDocument.ReadOnly && !mPlugin.App.ActiveDocument.Saved)
                    P4Operations.EditFile(mPlugin.OutputPane, mPlugin.App.ActiveDocument.FullName);
            }

        }
    }
}
