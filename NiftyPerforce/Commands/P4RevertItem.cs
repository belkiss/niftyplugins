// Copyright (C) 2006-2017 Jim Tilander, 2017-2024 Lambert Clara. See the COPYING file in the project root for full license information.
using System.Windows.Forms;
using Aurora;
using EnvDTE;

namespace NiftyPerforce.Commands
{
    internal sealed class P4RevertItem : ItemCommandBase
    {
        private readonly bool _onlyUnchanged;

        public P4RevertItem(Plugin plugin, string canonicalName, bool onlyUnchanged)
            : base("RevertItem", canonicalName, plugin, true, true, onlyUnchanged ? PackageIds.NiftyRevertUnchanged : PackageIds.NiftyRevert)
        {
            _onlyUnchanged = onlyUnchanged;
        }

        public override void OnExecute(SelectedItem item, string fileName)
        {
            if (!_onlyUnchanged)
            {
                string message = "You are about to revert the file '" + fileName + "'. Do you want to do this?";
                if (MessageBox.Show(message, "Revert File?", MessageBoxButtons.YesNo) != DialogResult.Yes)
                    return;
            }

            P4Operations.RevertFile(fileName, _onlyUnchanged);
        }
    }
}
