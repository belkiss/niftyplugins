// Copyright (C) 2006-2017 Jim Tilander, 2017-2023 Lambert Clara. See the COPYING file in the project root for full license information.
using Aurora;
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

        public override void OnExecute(SelectedItem item, string fileName)
        {
            if (!TryGetDirectoryName(fileName, out string? dirname))
                return;

            if (_mMainLine)
            {
                var options = (OptionsDialogPage)Plugin.Options;
                fileName = P4Operations.RemapToMain(fileName, options.MainLinePath);
            }

            P4Operations.TimeLapseView(dirname!, fileName);
        }
    }
}
