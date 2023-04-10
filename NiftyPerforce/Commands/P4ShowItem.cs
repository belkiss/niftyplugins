// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using Aurora;
using EnvDTE;

namespace NiftyPerforce
{
    internal sealed class P4ShowItem : ItemCommandBase
    {
        public P4ShowItem(Plugin plugin, string canonicalName)
            : base("ShowItem", canonicalName, plugin, true, true, PackageIds.NiftyShow)
        {
        }

        public override void OnExecute(SelectedItem item, string fileName)
        {
            P4Operations.P4VShowFile(fileName);
        }
    }
}
