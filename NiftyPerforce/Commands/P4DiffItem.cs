// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using Aurora;
using EnvDTE;

namespace NiftyPerforce
{
    internal class P4DiffItem : ItemCommandBase
    {
        public P4DiffItem(Plugin plugin, string canonicalName)
            : base("DiffItem", canonicalName, plugin, true, true, PackageIds.NiftyDiff)
        {
        }

        public override void OnExecute(SelectedItem item, string fileName)
        {
            P4Operations.DiffFile(fileName);
        }
    }
}
