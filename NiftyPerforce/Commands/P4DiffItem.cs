// Copyright (C) 2006-2017 Jim Tilander, 2017-2023 Lambert Clara. See the COPYING file in the project root for full license information.
using Aurora;
using EnvDTE;

namespace NiftyPerforce.Commands
{
    internal sealed class P4DiffItem : ItemCommandBase
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
