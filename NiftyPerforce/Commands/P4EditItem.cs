// SPDX-FileCopyrightText: 2006-2017 Jim Tilander
// SPDX-FileCopyrightText: 2017-2026 Lambert Clara
//
// SPDX-License-Identifier: MIT

using EnvDTE;

namespace NiftyPerforce.Commands
{
    internal sealed class P4EditItem : ItemCommandBase
    {
        public P4EditItem(Plugin plugin, string canonicalName)
            : base("EditItem", canonicalName, plugin, true, true, PackageIds.NiftyEdit)
        {
        }

        protected override void OnExecute(SelectedItem item, string filePath)
        {
            Plugin.P4Operations.EditFile(filePath, true);
        }
    }
}
