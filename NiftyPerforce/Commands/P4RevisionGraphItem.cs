// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using EnvDTE;
using System.IO;
using Aurora;

namespace NiftyPerforce
{
    class P4RevisionGraphItem : ItemCommandBase
    {
		private bool mMainLine;

        public P4RevisionGraphItem(Plugin plugin, string canonicalName, bool mainLine)
            : base("P4RevisionGraphItem", canonicalName, plugin, true, true, mainLine ? PackageIds.NiftyRevisionGraphMain : PackageIds.NiftyRevisionGraph)
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

			P4Operations.RevisionGraph(pane, dirname, fileName);
        }
    }
}
