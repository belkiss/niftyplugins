// Copyright (C) 2006-2017 Jim Tilander, 2017-2023 Lambert Clara. See the COPYING file in the project root for full license information.
using System.Reflection;
using System.Runtime.InteropServices;

#if NIFTY_LEGACY
using NiftyPerforce.Manifests.Legacy;
#else
using NiftyPerforce.Manifests;
#endif

[assembly: AssemblyTitle(Vsix.Name)]
[assembly: AssemblyDescription(Vsix.Description)]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany(Vsix.Author)]
[assembly: AssemblyProduct(Vsix.Name)]
[assembly: AssemblyCopyright(Vsix.Author)]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

[assembly: AssemblyVersion(Vsix.Version)]
[assembly: AssemblyFileVersion(Vsix.Version)]
