// SPDX-FileCopyrightText: 2006-2017 Jim Tilander
// SPDX-FileCopyrightText: 2017-2026 Lambert Clara
//
// SPDX-License-Identifier: MIT

using EnvDTE;

namespace NiftyPerforce.Commands
{
    internal sealed class P4RevisionGraphItem : ItemCommandBase
    {
        private readonly bool _mMainLine;

        public P4RevisionGraphItem(Plugin plugin, string canonicalName, bool mainLine)
            : base("P4RevisionGraphItem", canonicalName, plugin, true, true, mainLine ? PackageIds.NiftyRevisionGraphMain : PackageIds.NiftyRevisionGraph)
        {
            _mMainLine = mainLine;
        }

        protected override void OnExecute(SelectedItem item, string filePath)
        {
            if (!TryGetDirectoryName(filePath, out string? dirname))
                return;

            if (_mMainLine)
            {
                var options = (OptionsDialogPage)Plugin.Options;
                filePath = Plugin.P4Operations.RemapToMain(filePath, options.MainLinePath);
            }

            Plugin.P4Operations.RevisionGraph(dirname!, filePath);
        }
    }
}
