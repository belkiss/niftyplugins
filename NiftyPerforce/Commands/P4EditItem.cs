// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using EnvDTE;
using Aurora;

namespace NiftyPerforce
{
	class P4EditItem : ItemCommandBase
	{
		public P4EditItem(Plugin plugin, string canonicalName)
			: base("EditItem", canonicalName, plugin, true, true, PackageIds.NiftyEdit)
		{
		}

        public override void OnExecute(SelectedItem item, string fileName, OutputWindowPane pane)
        {
			P4Operations.EditFile(pane, fileName);
        }
	}
}
