// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using EnvDTE;
using Aurora;

namespace NiftyPerforce
{
	class P4DiffItem : ItemCommandBase
	{
		public P4DiffItem(Plugin plugin, string canonicalName)
			: base("DiffItem", canonicalName, plugin, true, true, PackageIds.NiftyDiff)
		{
		}

        public override void OnExecute(SelectedItem item, string fileName, OutputWindowPane pane)
        {
            P4Operations.DiffFile(pane, fileName);
        }
	}
}
