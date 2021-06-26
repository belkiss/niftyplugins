// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using System.IO;
using Aurora;
using EnvDTE;

namespace NiftyPerforce
{
    internal class P4RevisionHistoryItem : ItemCommandBase
    {
        private readonly bool mMainLine;

        public P4RevisionHistoryItem(Plugin plugin, string canonicalName, bool mainLine)
            : base("RevisionHistoryItem", canonicalName, plugin, true, true, mainLine ? PackageIds.NiftyHistoryMain : PackageIds.NiftyHistory)
        {
            mMainLine = mainLine;
        }

        public override void OnExecute(SelectedItem item, string fileName, OutputWindowPane pane)
        {
            string dirname = Path.GetDirectoryName(fileName);

            if (mMainLine)
            {
                var options = (NiftyPerforce.Config)Plugin.Options;
                fileName = P4Operations.RemapToMain(fileName, options.MainLinePath);
            }

            P4Operations.RevisionHistoryFile(pane, dirname, fileName);
        }
    }
}
