// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using System;
using Aurora;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace NiftyPerforce
{
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

        ////////////////////////////////////////////////////////////////////
        // default implementation for the pure methods, return OK
        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) { return VSConstants.S_OK; }
        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew) { return VSConstants.S_OK; }
        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) { return VSConstants.S_OK; }
        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) { return VSConstants.S_OK; }
        public int OnAfterSave(uint docCookie) { return VSConstants.S_OK; }
        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame) { return VSConstants.S_OK; }
        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) { return VSConstants.S_OK; }
    }

    internal sealed class AutoCheckoutOnSave : PreCommandFeature
    {
        internal Lazy<RunningDocumentTable>? _rdt;
        internal uint _rdte;
        private readonly IServiceProvider _serviceProvider;

        public AutoCheckoutOnSave(Plugin plugin, IServiceProvider serviceProvider)
            : base(plugin, "AutoCheckoutOnSave")
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _serviceProvider = serviceProvider;
            ((Config)mPlugin.Options).OnApplyEvent += (s, e) => RegisterEvents();
            RegisterEvents();
        }

        private bool RDTAdvised => _rdt != null;

        private void RegisterEvents()
        {
            if (((Config)mPlugin.Options).AutoCheckoutOnSave)
            {
                if (!RDTAdvised)
                {
                    Log.Info("Adding handlers for automatically checking out dirty files when you save");
                    _rdt = new Lazy<RunningDocumentTable>(() => new RunningDocumentTable(_serviceProvider));
                    _rdte = _rdt.Value.Advise(new RunningDocTableEvents(this));
                }
            }
            else if (RDTAdvised)
            {
                Log.Info("Removing handlers for automatically checking out dirty files when you save");
                _rdt!.Value.Unadvise(_rdte);
                _rdt = null;
            }
        }

        internal bool OnBeforeSave(uint docCookie)
        {
            if (!RDTAdvised)
                return false;

            RunningDocumentInfo runningDocumentInfo = _rdt!.Value.GetDocumentInfo(docCookie);
            string filename = runningDocumentInfo.Moniker;
            return P4Operations.EditFileImmediate(filename);
        }
    }
}
