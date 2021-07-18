// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Aurora;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace NiftyPerforce
{
    internal class TrackProjectDocumentsEvents : IVsTrackProjectDocumentsEvents2
    {
        private readonly AutoAddDelete _autoAddDelete;
        public bool AutoAdd { get; private set; }
        public bool AutoDelete { get; private set; }

        public TrackProjectDocumentsEvents(AutoAddDelete autoAddDelete)
        {
            _autoAddDelete = autoAddDelete;
        }

        public void UpdateAutoAddDelete(bool autoAdd, bool autoDelete)
        {
            AutoAdd = autoAdd;
            AutoDelete = autoDelete;
        }

        #region Add
        public int OnAfterAddFilesEx(int cProjects, int cFiles, IVsProject[] rgpProjects, int[] rgFirstIndices, string[] rgpszMkDocuments, VSADDFILEFLAGS[] rgFlags)
        {
            if (AutoAdd)
                _autoAddDelete.AddFilesAndDirectories(rgpszMkDocuments, Enumerable.Empty<string>());
            return VSConstants.S_OK;
        }

        public int OnAfterAddDirectoriesEx(int cProjects, int cDirectories, IVsProject[] rgpProjects, int[] rgFirstIndices, string[] rgpszMkDocuments, VSADDDIRECTORYFLAGS[] rgFlags)
        {
            if (AutoAdd)
                _autoAddDelete.AddFilesAndDirectories(Enumerable.Empty<string>(), rgpszMkDocuments);
            return VSConstants.S_OK;
        }

        // do nothing during the queries, we'll add the files to perforce during the AfterAdd
        public int OnQueryAddFiles(IVsProject pProject, int cFiles, string[] rgpszMkDocuments, VSQUERYADDFILEFLAGS[] rgFlags, VSQUERYADDFILERESULTS[] pSummaryResult, VSQUERYADDFILERESULTS[] rgResults) { return VSConstants.S_OK; }
        public int OnQueryAddDirectories(IVsProject pProject, int cDirectories, string[] rgpszMkDocuments, VSQUERYADDDIRECTORYFLAGS[] rgFlags, VSQUERYADDDIRECTORYRESULTS[] pSummaryResult, VSQUERYADDDIRECTORYRESULTS[] rgResults) { return VSConstants.S_OK; }
        #endregion

        #region Remove
        private List<string> _pendingFilesToRemove;
        public int OnAfterRemoveFiles(int cProjects, int cFiles, IVsProject[] rgpProjects, int[] rgFirstIndices, string[] rgpszMkDocuments, VSREMOVEFILEFLAGS[] rgFlags)
        {
            if (AutoDelete)
                _autoAddDelete.RemoveFiles(rgpszMkDocuments);
            return VSConstants.S_OK;
        }

        // we need to make the list of files that were in the directories *before* they are removed
        public int OnQueryRemoveDirectories(IVsProject pProject, int cDirectories, string[] rgpszMkDocuments, VSQUERYREMOVEDIRECTORYFLAGS[] rgFlags, VSQUERYREMOVEDIRECTORYRESULTS[] pSummaryResult, VSQUERYREMOVEDIRECTORYRESULTS[] rgResults)
        {
            if (AutoDelete)
            {
                _pendingFilesToRemove = new List<string>();
                _pendingFilesToRemove.AddRange(AutoAddDelete.GatherFilesInDirectories(rgpszMkDocuments));
            }

            return VSConstants.S_OK;
        }

        // and mark them for delete after the removal was done by the IDE
        public int OnAfterRemoveDirectories(int cProjects, int cDirectories, IVsProject[] rgpProjects, int[] rgFirstIndices, string[] rgpszMkDocuments, VSREMOVEDIRECTORYFLAGS[] rgFlags)
        {
            if (AutoDelete)
            {
                Trace.Assert(_pendingFilesToRemove != null);
                _autoAddDelete.RemoveFiles(_pendingFilesToRemove);
                _pendingFilesToRemove = null;
            }

            return VSConstants.S_OK;
        }

        // do nothing during the query of files, we'll remove thel from perforce during the AfterRemove since we'll get the list again
        public int OnQueryRemoveFiles(IVsProject pProject, int cFiles, string[] rgpszMkDocuments, VSQUERYREMOVEFILEFLAGS[] rgFlags, VSQUERYREMOVEFILERESULTS[] pSummaryResult, VSQUERYREMOVEFILERESULTS[] rgResults) { return VSConstants.S_OK; }
        #endregion

        #region Rename
        public int OnQueryRenameFiles(IVsProject pProject, int cFiles, string[] rgszMkOldNames, string[] rgszMkNewNames, VSQUERYRENAMEFILEFLAGS[] rgFlags, VSQUERYRENAMEFILERESULTS[] pSummaryResult, VSQUERYRENAMEFILERESULTS[] rgResults) { return VSConstants.S_OK; }
        public int OnQueryRenameDirectories(IVsProject pProject, int cDirs, string[] rgszMkOldNames, string[] rgszMkNewNames, VSQUERYRENAMEDIRECTORYFLAGS[] rgFlags, VSQUERYRENAMEDIRECTORYRESULTS[] pSummaryResult, VSQUERYRENAMEDIRECTORYRESULTS[] rgResults) { return VSConstants.S_OK; }

        public int OnAfterRenameFiles(int cProjects, int cFiles, IVsProject[] rgpProjects, int[] rgFirstIndices, string[] rgszMkOldNames, string[] rgszMkNewNames, VSRENAMEFILEFLAGS[] rgFlags)
        {
            // TODO: implement me!
            return VSConstants.S_OK;
        }

        public int OnAfterRenameDirectories(int cProjects, int cDirs, IVsProject[] rgpProjects, int[] rgFirstIndices, string[] rgszMkOldNames, string[] rgszMkNewNames, VSRENAMEDIRECTORYFLAGS[] rgFlags)
        {
            // TODO: implement me!
            return VSConstants.S_OK;
        }
        #endregion

        public int OnAfterSccStatusChanged(int cProjects, int cFiles, IVsProject[] rgpProjects, int[] rgFirstIndices, string[] rgpszMkDocuments, uint[] rgdwSccStatus) { return VSConstants.S_OK; }
    }

    // Handles registration and events for add/delete files and projects.
    internal class AutoAddDelete : PreCommandFeature
    {
        private readonly IServiceProvider _serviceProvider;
        private IVsTrackProjectDocuments2 _tpd;
        private TrackProjectDocumentsEvents _tpde;
        private uint _tpdeCookie = 0;

        private bool TPDEAdvised => _tpde != null;

        public AutoAddDelete(Plugin plugin, IServiceProvider serviceProvider)
            : base(plugin, "AutoAddDelete")
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            _serviceProvider = serviceProvider;

            ((Config)mPlugin.Options).OnApplyEvent += RegisterEvents;
            RegisterEvents();
        }

        private void RegisterEvents(object sender = null, EventArgs e = null)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (((Config)mPlugin.Options).AutoAdd || ((Config)mPlugin.Options).AutoDelete)
            {
                if (_tpd == null)
                    _tpd = _serviceProvider.GetService(typeof(SVsTrackProjectDocuments)) as IVsTrackProjectDocuments2;

                string action;
                if (!TPDEAdvised)
                {
                    _tpde = new TrackProjectDocumentsEvents(this);
                    action = "Added handlers";
                    ErrorHandler.ThrowOnFailure(_tpd.AdviseTrackProjectDocumentsEvents(_tpde, out _tpdeCookie));
                }
                else
                {
                    action = "Updated handlers";
                }

                _tpde.UpdateAutoAddDelete(((Config)mPlugin.Options).AutoAdd, ((Config)mPlugin.Options).AutoDelete);

                Log.Info(
                    "{0} to automatically {1} files {2} perforce as you {1} them {2} the project",
                    action,
                    _tpde.AutoAdd && _tpde.AutoDelete ? "add/remove" : (_tpde.AutoAdd ? "add" : "remove"),
                    _tpde.AutoAdd && _tpde.AutoDelete ? "to/from" : (_tpde.AutoAdd ? "to" : "from")
                );
            }
            else if (TPDEAdvised)
            {
                ErrorHandler.ThrowOnFailure(_tpd.UnadviseTrackProjectDocumentsEvents(_tpdeCookie));
                Log.Info(
                    "Removed handlers to automatically {0} files {1} perforce as you {0} them {1} the project",
                    _tpde.AutoAdd && _tpde.AutoDelete ? "add/remove" : (_tpde.AutoAdd ? "add" : "remove"),
                    _tpde.AutoAdd && _tpde.AutoDelete ? "to/from" : (_tpde.AutoAdd ? "to" : "from")
                );
                _tpde = null;
            }
        }

        internal static IEnumerable<string> GatherFilesInDirectories(IEnumerable<string> directories)
        {
            foreach (var directory in directories)
                foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                    yield return file;
        }

        internal void AddFilesAndDirectories(IEnumerable<string> files, IEnumerable<string> directories)
        {
            if (!((Config)mPlugin.Options).AutoAdd)
                return;

            var allFiles = new List<string>();
            allFiles.AddRange(files);
            foreach (var directory in directories)
                allFiles.AddRange(Directory.GetFiles(directory, "*", SearchOption.AllDirectories));

            var orderedUniqueFileList = allFiles.OrderBy(f => f).Distinct();
            // TODO: this should be done in one p4 call
            foreach (string file in orderedUniqueFileList)
                P4Operations.AddFile(file);
        }

        internal void RemoveFiles(IEnumerable<string> files)
        {
            if (!((Config)mPlugin.Options).AutoDelete)
                return;

            var orderedUniqueFileList = files.OrderBy(f => f).Distinct();

            // TODO: this should be done in one p4 call
            foreach (string file in orderedUniqueFileList)
                P4Operations.DeleteFile(file);
        }

        internal void OnProjectAdded(Project project)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            P4Operations.EditFile(mPlugin.App.Solution.FullName, false);
            P4Operations.AddFile(project.FullName);
            // TODO: [jt] We should if the operation is not a add new project but rather a add existing project
            //       step through all the project items and add them to perforce. Or maybe we want the user
            //       to do this herself?
        }

        internal void OnProjectRemoved(Project project)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            P4Operations.EditFile(mPlugin.App.Solution.FullName, false);
            P4Operations.DeleteFile(project.FullName);
            // TODO: [jt] Do we want to automatically delete the items from perforce here?
        }
    }
}
