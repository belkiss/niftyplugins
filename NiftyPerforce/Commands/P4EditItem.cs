// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using Aurora;
using EnvDTE;

namespace NiftyPerforce
{
    internal class P4EditItem : ItemCommandBase
    {
        public P4EditItem(Plugin plugin, string canonicalName)
            : base("EditItem", canonicalName, plugin, true, true, PackageIds.NiftyEdit)
        {
        }

        public override void OnExecute(SelectedItem item, string fileName)
        {
            P4Operations.EditFile(fileName, true);
        }
    }
}
