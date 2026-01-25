// SPDX-FileCopyrightText: 2006-2017 Jim Tilander
// SPDX-FileCopyrightText: 2017-2026 Lambert Clara
//
// SPDX-License-Identifier: MIT

using EnvDTE;

namespace NiftyPerforce.Commands
{
    internal sealed class P4TimeLapseItem : ItemCommandBase
    {
        private readonly bool _mMainLine;

        public P4TimeLapseItem(Plugin plugin, string canonicalName, bool inMainLine)
            : base("TimeLapseItem", canonicalName, plugin, true, true, inMainLine ? PackageIds.NiftyTimeLapseMain : PackageIds.NiftyTimeLapse)
        {
            _mMainLine = inMainLine;
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

            Plugin.P4Operations.TimeLapseView(dirname!, filePath);
        }
    }
}
