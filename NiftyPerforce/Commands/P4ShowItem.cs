// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.
using Aurora;
using EnvDTE;

namespace NiftyPerforce.Commands
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
