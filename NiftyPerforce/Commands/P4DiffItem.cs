// SPDX-FileCopyrightText: 2006-2017 Jim Tilander
// SPDX-FileCopyrightText: 2017-2026 Lambert Clara
//
// SPDX-License-Identifier: MIT

using EnvDTE;

namespace NiftyPerforce.Commands
{
    internal sealed class P4DiffItem : ItemCommandBase
    {
        public P4DiffItem(Plugin plugin, string canonicalName)
            : base("DiffItem", canonicalName, plugin, true, true, PackageIds.NiftyDiff)
        {
        }

        protected override void OnExecute(SelectedItem item, string filePath)
        {
            Plugin.P4Operations.DiffFile(filePath);
        }
    }
}
