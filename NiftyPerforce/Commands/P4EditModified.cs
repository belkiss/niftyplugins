// SPDX-FileCopyrightText: 2006-2017 Jim Tilander
// SPDX-FileCopyrightText: 2017-2026 Lambert Clara
//
// SPDX-License-Identifier: MIT

using EnvDTE;
using NiftyPerforce.Core;

namespace NiftyPerforce.Commands
{
    internal sealed class P4EditModified : CommandBase
    {
        public P4EditModified(Plugin plugin, string canonicalName)
            : base("EditModified", canonicalName, plugin, PackageIds.NiftyEditModified)
        {
        }

        public override bool OnCommand()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            Log.Info("P4EditModified : looking for modified files");

            if (!Plugin.App.Solution.Saved)
            {
                Log.Info($"P4EditModified : solution {Plugin.App.Solution.FullName} was dirty, checkout");
                Plugin.P4Operations.EditFile(Plugin.App.Solution.FullName, false);
            }

            foreach (Project p in Plugin.App.Solution.Projects)
            {
                if (!p.Saved)
                {
                    Log.Info($"P4EditModified : project {p.FullName} was dirty, checkout");
                    Plugin.P4Operations.EditFile(p.FullName, false);
                }
            }

            foreach (Document doc in Plugin.App.Documents)
            {
                if (!doc.Saved)
                {
                    Log.Info($"P4EditModified : document {doc.FullName} was dirty, checkout");
                    Plugin.P4Operations.EditFile(doc.FullName, false);
                }
            }

            return true;
        }

        public override bool IsEnabled()
        {
            return true;
        }
    }
}
