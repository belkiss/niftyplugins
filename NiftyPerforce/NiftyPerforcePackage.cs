// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using NiftyPerforce.Commands;
using NiftyPerforce.Core;
using Task = System.Threading.Tasks.Task;

#if NIFTY_LEGACY
using NiftyPerforce.Manifests.Legacy;
#else
using NiftyPerforce.Manifests;
#endif

namespace NiftyPerforce
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    // Declare that resources for the package are to be found in the managed assembly resources, and not in a satellite dll
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", Vsix.Version, IconResourceID = 400)] // Register the product to be listed in About box
    [ProvideAutoLoad(Microsoft.VisualStudio.VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(OptionsDialogPage), "Source Control", Vsix.Name, 106, 107, false)]
    [Guid(PackageGuids.guidNiftyPerforcePackageString)]
    public sealed class NiftyPerforcePackage : AsyncPackage
    {
        private Plugin? _plugin;
        private CommandRegistry? _commandRegistry;

        private async Task<TReturnType> GetServiceAsync<TServiceType, TReturnType>() => (TReturnType)await GetServiceAsync(typeof(TServiceType));

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">Init progress.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            DTE2 dte2Service = await GetServiceAsync<DTE, DTE2>();

            if (!(await GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService oleMenuCommandService))
            {
                throw new ArgumentException("Impossible to fetch OleMenuCommand service");
            }

            // Switches to the UI thread in order to consume some services used in command initialization
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Initialize the logging system.
            if (Log.HandlerCount == 0)
            {
#if DEBUG
                Log.AddHandler(new DebugLogHandler());
#endif
                Log.AddHandler(new VisualStudioLogHandler("NiftyPerforce", this));
                Log.Prefix = "NiftyPerforce";
            }

            // Show where we are and when we were compiled...
            var niftyAssembly = Assembly.GetExecutingAssembly();
            Version? version = niftyAssembly.GetName().Version;
            string versionString = string.Empty;
            if (version != null)
            {
                versionString = string.Join(".", version.Major, version.Minor, version.Build);
                string? informationalVersion = niftyAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrEmpty(informationalVersion))
                    versionString += " " + informationalVersion;
            }

            Log.Info(
                "NiftyPerforce{0} v{1} compiled on {2}",
#if DEBUG
                " (Debug!)",
#else
                string.Empty,
#endif
                versionString,
                System.IO.File.GetLastWriteTime(niftyAssembly.Location).ToString(CultureInfo.CurrentCulture));

            Log.Debug("Location: '{0}'", niftyAssembly?.Location ?? "unknown");

            // Now we can take care of registering ourselves and all our commands and hooks.
            Log.Debug("Booting up...");

            var config = (OptionsDialogPage)GetDialogPage(typeof(OptionsDialogPage));
            _plugin = new Plugin(dte2Service, oleMenuCommandService, config, new P4Operations());
            void ApplyOptions()
            {
                _plugin.P4Operations.SetOptions(config.IgnoreReadOnlyOnEdit, config.UseSystemEnv, config.PreferredLookupSource, config.Port, config.Client, config.Username);
            }

            config.OnApplyEvent += (s, e) => ApplyOptions();

            InitCommandRegistry();

            _plugin.AddFeature(new EventHandlers.AutoAddDelete(_plugin));
            _plugin.AddFeature(new EventHandlers.AutoCheckoutProject(_plugin));
            _plugin.AddFeature(new EventHandlers.AutoCheckoutTextEdit(_plugin));
            _plugin.AddFeature(new EventHandlers.AutoCheckoutOnSave(_plugin, this));

            _plugin.P4Operations.CheckInstalledFiles(new P4Utils(new FileSystem(), new DefaultRegistryService()));
            ApplyOptions();

            AsyncProcess.Init();

            Log.Debug("Initialization complete.");
        }

        private void InitCommandRegistry()
        {
            Trace.Assert(_plugin != null);
            Assumes.NotNull(_plugin);

            _commandRegistry = new CommandRegistry(_plugin!, new Guid(PackageGuids.guidNiftyPerforcePackageCmdSetString));

            // Add our command handlers for menu (commands must exist in the .vsct file)
            _commandRegistry.RegisterCommand(new P4EditModified(_plugin!, "NiftyEditModified"));
            _commandRegistry.RegisterCommand(new P4EditItem(_plugin!, "NiftyEdit"));
            _commandRegistry.RegisterCommand(new P4DiffItem(_plugin!, "NiftyDiff"));
            _commandRegistry.RegisterCommand(new P4RevisionHistoryItem(_plugin!, "NiftyHistory", false));
            _commandRegistry.RegisterCommand(new P4RevisionHistoryItem(_plugin!, "NiftyHistoryMain", true));
            _commandRegistry.RegisterCommand(new P4TimeLapseItem(_plugin!, "NiftyTimeLapse", false));
            _commandRegistry.RegisterCommand(new P4TimeLapseItem(_plugin!, "NiftyTimeLapseMain", true));
            _commandRegistry.RegisterCommand(new P4RevisionGraphItem(_plugin!, "NiftyRevisionGraph", false));
            _commandRegistry.RegisterCommand(new P4RevisionGraphItem(_plugin!, "NiftyRevisionGraphMain", true));
            _commandRegistry.RegisterCommand(new P4RevertItem(_plugin!, "NiftyRevert", false));
            _commandRegistry.RegisterCommand(new P4RevertItem(_plugin!, "NiftyRevertUnchanged", true));
            _commandRegistry.RegisterCommand(new P4ShowItem(_plugin!, "NiftyShow"));
            _commandRegistry.RegisterCommand(new CopyPath(_plugin!, "NiftyCopyFileName", CopyPath.Mode.FileName));
            _commandRegistry.RegisterCommand(new CopyPath(_plugin!, "NiftyCopyRelativePath", CopyPath.Mode.RelativePath));
        }
    }
}
