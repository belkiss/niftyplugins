// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using System.IO;
using Aurora;
using EnvDTE;

namespace NiftyPerforce
{
    internal class P4TimeLapseItem : ItemCommandBase
    {
        private readonly bool mMainLine;

        public P4TimeLapseItem(Plugin plugin, string canonicalName, bool inMainLine)
            : base("TimeLapseItem", canonicalName, plugin, true, true, inMainLine ? PackageIds.NiftyTimeLapseMain : PackageIds.NiftyTimeLapse)
        {
            mMainLine = inMainLine;
        }

        public override void OnExecute(SelectedItem item, string fileName, OutputWindowPane pane)
        {
            string dirname = Path.GetDirectoryName(fileName);

            if (mMainLine)
            {
                var options = (NiftyPerforce.Config)Plugin.Options;
                fileName = P4Operations.RemapToMain(fileName, options.MainLinePath);
            }

            P4Operations.TimeLapseView(pane, dirname, fileName);
        }
    }
}
