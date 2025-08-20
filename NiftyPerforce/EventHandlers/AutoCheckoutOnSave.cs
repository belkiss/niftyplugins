// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using NiftyPerforce.Core;

namespace NiftyPerforce.EventHandlers
{
    internal sealed class AutoCheckoutOnSave : PreCommandFeature
    {
        private readonly IServiceProvider _serviceProvider;
        private RunningDocumentTable? _rdt;
        private uint _rdte;
        private ITextDocumentFactoryService? _textDocumentFactoryService;
        private ConcurrentDictionary<string, bool>? _textDocuments;

        public AutoCheckoutOnSave(Plugin plugin, IServiceProvider serviceProvider)
            : base(plugin, "AutoCheckoutOnSave")
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _serviceProvider = serviceProvider;
            ((OptionsDialogPage)Plugin.Options).OnApplyEvent += (s, e) => RegisterEvents();
            RegisterEvents();
        }

        private bool RdtAdvised => _rdt != null;

        private void RegisterEvents()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (((OptionsDialogPage)Plugin.Options).AutoCheckoutOnSave)
            {
                if (!RdtAdvised)
                {
                    Log.Info("Adding handlers for automatically checking out dirty files when you save");
                    _rdt = new RunningDocumentTable(_serviceProvider);
                    _rdte = _rdt.Advise(new RunningDocTableEvents(this));

                    var componentModel = (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel));
                    Assumes.Present(componentModel);
                    _textDocumentFactoryService = componentModel.GetService<ITextDocumentFactoryService>();

                    _textDocuments = new ConcurrentDictionary<string, bool>();

                    _textDocumentFactoryService.TextDocumentCreated += OnTextDocumentCreated;
                    _textDocumentFactoryService.TextDocumentDisposed += OnTextDocumentDisposed;
                }
            }
            else if (RdtAdvised)
            {
                Log.Info("Removing handlers for automatically checking out dirty files when you save");

                _textDocumentFactoryService!.TextDocumentDisposed -= OnTextDocumentDisposed;
                _textDocumentFactoryService!.TextDocumentCreated -= OnTextDocumentCreated;

                _textDocuments = null;

                _rdt!.Unadvise(_rdte);
                _rdt = null;
            }
        }

        internal bool OnBeforeSave(uint docCookie)
        {
            if (!TryGetFilePath(docCookie, out string? filePath))
                return false;

            bool forceEdit = _textDocuments?.TryRemove(filePath!, out _) ?? false;
            if (forceEdit)
                Log.Info("Force edit because '{0}' was probably find / replaced", filePath!);

            return Plugin.P4Operations.EditFileImmediate(filePath!, forceEdit);
        }

        internal bool TryGetFilePath(uint docCookie, out string? filePath)
        {
            filePath = RdtAdvised ? _rdt!.GetDocumentInfo(docCookie).Moniker : null;
            return !string.IsNullOrEmpty(filePath);
        }

        internal void RegisterTextDocument(string filePath)
        {
            bool readOnly = File.Exists(filePath) && File.GetAttributes(filePath).HasFlag(FileAttributes.ReadOnly);
            if (readOnly)
                _textDocuments!.TryAdd(filePath, true);
        }

        private void OnTextDocumentCreated(object sender, TextDocumentEventArgs e)
        {
            RegisterTextDocument(e.TextDocument.FilePath);
        }

        private void OnTextDocumentDisposed(object sender, TextDocumentEventArgs e)
        {
            _textDocuments!.TryRemove(e.TextDocument.FilePath, out _);
        }
    }

    // Create a class to retrieve the OnBeforeSave event from VS
    // http://schmalls.com/2015/01/19/adventures-in-visual-studio-extension-development-part-2
    internal sealed class RunningDocTableEvents : IVsRunningDocTableEvents3
    {
        private readonly AutoCheckoutOnSave _autoCheckoutOnSave;

        public RunningDocTableEvents(AutoCheckoutOnSave autoCheckoutOnSave)
        {
            _autoCheckoutOnSave = autoCheckoutOnSave;
        }

        public int OnBeforeSave(uint docCookie)
        {
            _autoCheckoutOnSave.OnBeforeSave(docCookie);
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            if (((__VSRDTATTRIB2)grfAttribs & __VSRDTATTRIB2.RDTA_DocDataIsReadOnly) != 0)
            {
                if (_autoCheckoutOnSave.TryGetFilePath(docCookie, out string? filePath))
                {
                    _autoCheckoutOnSave.RegisterTextDocument(filePath!);
                }
            }

            return VSConstants.S_OK;
        }

        ////////////////////////////////////////////////////////////////////
        // default implementation for the pure methods, return OK
        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) => VSConstants.S_OK;

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) => VSConstants.S_OK;

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRdtLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => VSConstants.S_OK;

        public int OnAfterSave(uint docCookie) => VSConstants.S_OK;

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame) => VSConstants.S_OK;

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRdtLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => VSConstants.S_OK;
    }
}
