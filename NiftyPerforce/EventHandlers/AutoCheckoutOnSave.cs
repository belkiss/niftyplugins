// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using System;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NiftyPerforce;

namespace Aurora
{
	namespace NiftyPerforce
	{
		// Create a class to retrieve the OnBeforeSave event from VS
		// http://schmalls.com/2015/01/19/adventures-in-visual-studio-extension-development-part-2
		internal class RunningDocTableEvents : IVsRunningDocTableEvents3
		{
			private readonly AutoCheckoutOnSave autoCheckoutOnSave;

			public RunningDocTableEvents(AutoCheckoutOnSave autoCheckoutOnSave)
			{
				this.autoCheckoutOnSave = autoCheckoutOnSave;
			}

			public int OnBeforeSave(uint docCookie)
			{
				RunningDocumentInfo runningDocumentInfo = autoCheckoutOnSave._rdt.Value.GetDocumentInfo(docCookie);
				autoCheckoutOnSave.OnBeforeSave(runningDocumentInfo.Moniker);
				return VSConstants.S_OK;
			}

			////////////////////////////////////////////////////////////////////
			// default implementation for the pure methods, return OK
			public int OnAfterAttributeChange(uint docCookie, uint grfAttribs){return VSConstants.S_OK;}
			public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew){return VSConstants.S_OK;}
			public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame){return VSConstants.S_OK;}
			public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining){return VSConstants.S_OK;}
			public int OnAfterSave(uint docCookie){return VSConstants.S_OK;}
			public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame){return VSConstants.S_OK;}
			public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining){return VSConstants.S_OK;}
		}

		class AutoCheckoutOnSave : PreCommandFeature
		{
			internal Lazy<RunningDocumentTable> _rdt;
			internal uint _rdte;
			internal Lazy<Microsoft.VisualStudio.OLE.Interop.IServiceProvider> _sp;

			public AutoCheckoutOnSave(Plugin plugin)
				: base(plugin, "AutoCheckoutOnSave", "Automatically checks out files on save")
			{
				ThreadHelper.ThrowIfNotOnUIThread();
				((Config)mPlugin.Options).OnApplyEvent += RegisterEvents;
				RegisterEvents();
			}

			private bool RDTAdvised { get { return _sp != null || _rdt != null; } }

			private void RegisterEvents(object sender = null, EventArgs e = null)
			{
				ThreadHelper.ThrowIfNotOnUIThread();

				if (((Config)mPlugin.Options).AutoCheckoutOnSave)
				{
					if (!RDTAdvised)
					{
						Log.Info("Adding handlers for automatically checking out dirty files when you save");
						_sp = new Lazy<Microsoft.VisualStudio.OLE.Interop.IServiceProvider>(() => {
							ThreadHelper.ThrowIfNotOnUIThread();
							return Package.GetGlobalService(typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider)) as Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
						});
						_rdt = new Lazy<RunningDocumentTable>(() => new RunningDocumentTable(new ServiceProvider(_sp.Value)));
						_rdte = _rdt.Value.Advise(new RunningDocTableEvents(this));
					}
				}
				else if (RDTAdvised)
				{
					Log.Info("Removing handlers for automatically checking out dirty files when you save");
					_rdt.Value.Unadvise(_rdte);
					_rdt = null;
					_sp = null;
				}
			}

			internal void OnBeforeSave(string filename)
			{
				P4Operations.EditFileImmediate(mPlugin.OutputPane, filename);
			}

			private void EditProjectRecursive(Project p)
			{
				ThreadHelper.ThrowIfNotOnUIThread();

				if (!p.Saved)
					P4Operations.EditFileImmediate(mPlugin.OutputPane, p.FullName);

				if(p.ProjectItems == null)
					return;

				foreach(ProjectItem pi in p.ProjectItems)
				{
					if(pi.SubProject != null)
					{
						EditProjectRecursive(pi.SubProject);
					}
					else if(!pi.Saved)
					{
						for(short i = 0; i <= pi.FileCount; i++)
							P4Operations.EditFileImmediate(mPlugin.OutputPane, pi.get_FileNames(i));
					}
				}
			}
		}
	}
}
