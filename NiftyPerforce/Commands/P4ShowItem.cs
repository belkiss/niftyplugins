// SPDX-FileCopyrightText: 2006-2017 Jim Tilander
// SPDX-FileCopyrightText: 2017-2026 Lambert Clara
//
// SPDX-License-Identifier: MIT

using EnvDTE;

namespace NiftyPerforce.Commands
{
    internal sealed class P4ShowItem : ItemCommandBase
    {
        public P4ShowItem(Plugin plugin, string canonicalName)
            : base("ShowItem", canonicalName, plugin, true, true, PackageIds.NiftyShow)
        {
        }

        protected override void OnExecute(SelectedItem item, string filePath)
        {
            Plugin.P4Operations.P4VShowFile(filePath);
        }
    }
}
