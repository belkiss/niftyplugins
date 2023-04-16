// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using Aurora;
using EnvDTE;

namespace NiftyPerforce
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
                var options = (NiftyPerforce.Config)Plugin.Options;
                fileName = P4Operations.RemapToMain(fileName, options.MainLinePath);
            }

            P4Operations.TimeLapseView(dirname!, fileName);
        }
    }
}
