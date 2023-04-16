// Copyright (C) 2006-2017 Jim Tilander, 2017-2023 Lambert Clara. See the COPYING file in the project root for full license information.
using Aurora;
using EnvDTE;

namespace NiftyPerforce
{
    internal sealed class P4RevisionHistoryItem : ItemCommandBase
    {
        private readonly bool _mMainLine;

        public P4RevisionHistoryItem(Plugin plugin, string canonicalName, bool mainLine)
            : base("RevisionHistoryItem", canonicalName, plugin, true, true, mainLine ? PackageIds.NiftyHistoryMain : PackageIds.NiftyHistory)
        {
            _mMainLine = mainLine;
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

            P4Operations.RevisionHistoryFile(dirname!, fileName);
        }
    }
}
