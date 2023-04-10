// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using Aurora;
using EnvDTE;

namespace NiftyPerforce
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
                P4Operations.EditFile(Plugin.App.Solution.FullName, false);
            }

            foreach (Project p in Plugin.App.Solution.Projects)
            {
                if (!p.Saved)
                {
                    Log.Info($"P4EditModified : project {p.FullName} was dirty, checkout");
                    P4Operations.EditFile(p.FullName, false);
                }
            }

            foreach (Document doc in Plugin.App.Documents)
            {
                if (!doc.Saved)
                {
                    Log.Info($"P4EditModified : document {doc.FullName} was dirty, checkout");
                    P4Operations.EditFile(doc.FullName, false);
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
