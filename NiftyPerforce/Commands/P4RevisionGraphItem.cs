// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using Aurora;
using EnvDTE;

namespace NiftyPerforce
{
    internal sealed class P4RevisionGraphItem : ItemCommandBase
    {
        private readonly bool _mMainLine;

        public P4RevisionGraphItem(Plugin plugin, string canonicalName, bool mainLine)
            : base("P4RevisionGraphItem", canonicalName, plugin, true, true, mainLine ? PackageIds.NiftyRevisionGraphMain : PackageIds.NiftyRevisionGraph)
        {
            _mMainLine = mainLine;
        }

        public override void OnExecute(SelectedItem item, string fileName)
        {
            if (!TryGetDirectoryName(fileName, out var dirname))
                return;

            if (_mMainLine)
            {
                var options = (NiftyPerforce.Config)Plugin.Options;
                fileName = P4Operations.RemapToMain(fileName, options.MainLinePath);
            }

            P4Operations.RevisionGraph(dirname!, fileName);
        }
    }
}
