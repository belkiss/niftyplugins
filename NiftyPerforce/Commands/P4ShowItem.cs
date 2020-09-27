// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using EnvDTE;
using Aurora;

namespace NiftyPerforce
{
	class P4ShowItem : ItemCommandBase
	{
		public P4ShowItem(Plugin plugin, string canonicalName)
			: base("ShowItem", canonicalName, plugin, true, true, PackageIds.NiftyShow)
		{
		}

		public override void OnExecute(SelectedItem item, string fileName, OutputWindowPane pane)
		{
			P4Operations.P4VShowFile(pane, fileName);
		}
	}
}
