// Copyright (C) 2006-2017 Jim Tilander, 2017-2023 Lambert Clara. See the COPYING file in the project root for full license information.
using Aurora;
using EnvDTE;

namespace NiftyPerforce
{
    internal sealed class P4EditItem : ItemCommandBase
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
