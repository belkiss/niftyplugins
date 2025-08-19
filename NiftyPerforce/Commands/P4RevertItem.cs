// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.
using System.Windows.Forms;
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

        protected override void OnExecute(SelectedItem item, string filePath)
        {
            if (!_onlyUnchanged)
            {
                string message = "You are about to revert the file '" + filePath + "'. Do you want to do this?";
                if (MessageBox.Show(message, "Revert File?", MessageBoxButtons.YesNo) != DialogResult.Yes)
                    return;
            }

            Plugin.P4Operations.RevertFile(filePath, _onlyUnchanged ? P4Operations.RevertFileOptions.OnlyUnchanged : P4Operations.RevertFileOptions.None);
        }
    }
}
