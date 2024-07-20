// Copyright (C) 2006-2017 Jim Tilander, 2017-2024 Lambert Clara. See the COPYING file in the project root for full license information.

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Aurora;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.CommandBars;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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

        public NiftyPerforcePackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        private async Task<TReturnType> GetServiceAsync<TServiceType, TReturnType>() => (TReturnType)await GetServiceAsync(typeof(TServiceType));

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
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
            Version? version = niftyAssembly?.GetName().Version;
            string versionString = string.Empty;
            if (version != null)
            {
                versionString = string.Join(".", version.Major, version.Minor, version.Build);
                string? informationalVersion = niftyAssembly!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
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
                niftyAssembly != null ? System.IO.File.GetLastWriteTime(niftyAssembly.Location).ToString() : "unknown");

            Log.Debug("    Location '{0}'", niftyAssembly?.Location ?? "unknown");

            // Now we can take care of registering ourselves and all our commands and hooks.
            Log.Debug("Booting up...");
            Log.IncIndent();

            var config = (OptionsDialogPage)GetDialogPage(typeof(OptionsDialogPage));
            Singleton<OptionsDialogPage>.Instance = config;

#if NIFTY_LEGACY
            config.OnApplyEvent += (object sender, EventArgs e) =>
            {
                if (config.CleanLegacyNiftyCommands)
                {
                    Cleanup();
                    config.CleanLegacyNiftyCommands = false;
                }
            };
#endif

            _plugin = new Plugin(dte2Service, oleMenuCommandService, config);

            InitCommandRegistry();

            _plugin.AddFeature(new EventHandlers.AutoAddDelete(_plugin));
            _plugin.AddFeature(new EventHandlers.AutoCheckoutProject(_plugin));
            _plugin.AddFeature(new EventHandlers.AutoCheckoutTextEdit(_plugin));
            _plugin.AddFeature(new EventHandlers.AutoCheckoutOnSave(_plugin, this));

            P4Operations.CheckInstalledFiles();

            AsyncProcess.Init();

            Log.DecIndent();
            Log.Debug("Initialized...");
        }

        private void InitCommandRegistry()
        {
            Trace.Assert(_plugin != null);
            Assumes.NotNull(_plugin);

            _commandRegistry = new CommandRegistry(_plugin!, new Guid(PackageGuids.guidNiftyPerforcePackageCmdSetString));

            // Add our command handlers for menu (commands must exist in the .vsct file)
            _commandRegistry.RegisterCommand(new Commands.P4EditModified(_plugin!, "NiftyEditModified"));
            _commandRegistry.RegisterCommand(new Commands.P4EditItem(_plugin!, "NiftyEdit"));
            _commandRegistry.RegisterCommand(new Commands.P4DiffItem(_plugin!, "NiftyDiff"));
            _commandRegistry.RegisterCommand(new Commands.P4RevisionHistoryItem(_plugin!, "NiftyHistory", false));
            _commandRegistry.RegisterCommand(new Commands.P4RevisionHistoryItem(_plugin!, "NiftyHistoryMain", true));
            _commandRegistry.RegisterCommand(new Commands.P4TimeLapseItem(_plugin!, "NiftyTimeLapse", false));
            _commandRegistry.RegisterCommand(new Commands.P4TimeLapseItem(_plugin!, "NiftyTimeLapseMain", true));
            _commandRegistry.RegisterCommand(new Commands.P4RevisionGraphItem(_plugin!, "NiftyRevisionGraph", false));
            _commandRegistry.RegisterCommand(new Commands.P4RevisionGraphItem(_plugin!, "NiftyRevisionGraphMain", true));
            _commandRegistry.RegisterCommand(new Commands.P4RevertItem(_plugin!, "NiftyRevert", false));
            _commandRegistry.RegisterCommand(new Commands.P4RevertItem(_plugin!, "NiftyRevertUnchanged", true));
            _commandRegistry.RegisterCommand(new Commands.P4ShowItem(_plugin!, "NiftyShow"));
        }

        /// <summary>
        ///  Removes all installed legacy commands and controls.
        /// </summary>
        public void Cleanup()
        {
            Log.Info("Cleaning up all legacy nifty commands");

            ThreadHelper.ThrowIfNotOnUIThread();

            if (!(GetService(typeof(SVsProfferCommands)) is IVsProfferCommands3 profferCommands3))
                return;

            RemoveCommandBar("NiftyPerforceCmdBar", profferCommands3);
            RemoveCommandBar("NiftyPerforce", profferCommands3);

            RemoveCommand("NiftyConfig", profferCommands3);
            RemoveCommand("NiftyEditModified", profferCommands3);
            RemoveCommand("NiftyEdit", profferCommands3);
            RemoveCommand("NiftyEditItem", profferCommands3);
            RemoveCommand("NiftyEditSolution", profferCommands3);
            RemoveCommand("NiftyDiff", profferCommands3);
            RemoveCommand("NiftyDiffItem", profferCommands3);
            RemoveCommand("NiftyDiffSolution", profferCommands3);
            RemoveCommand("NiftyHistory", profferCommands3);
            RemoveCommand("NiftyHistoryMain", profferCommands3);
            RemoveCommand("NiftyHistoryItem", profferCommands3);
            RemoveCommand("NiftyHistoryItemMain", profferCommands3);
            RemoveCommand("NiftyHistorySolution", profferCommands3);
            RemoveCommand("NiftyTimeLapse", profferCommands3);
            RemoveCommand("NiftyTimeLapseMain", profferCommands3);
            RemoveCommand("NiftyTimeLapseItem", profferCommands3);
            RemoveCommand("NiftyTimeLapseItemMain", profferCommands3);
            RemoveCommand("NiftyRevisionGraph", profferCommands3);
            RemoveCommand("NiftyRevisionGraphMain", profferCommands3);
            RemoveCommand("NiftyRevisionGraphItem", profferCommands3);
            RemoveCommand("NiftyRevisionGraphItemMain", profferCommands3);
            RemoveCommand("NiftyRevert", profferCommands3);
            RemoveCommand("NiftyRevertItem", profferCommands3);
            RemoveCommand("NiftyRevertUnchanged", profferCommands3);
            RemoveCommand("NiftyRevertUnchangedItem", profferCommands3);
            RemoveCommand("NiftyShow", profferCommands3);
            RemoveCommand("NiftyShowItem", profferCommands3);
        }

        private void RemoveCommand(string name, IVsProfferCommands3 profferCommands3)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                Command? cmd = _plugin?.Commands.Item(name, -1);
                if (cmd != null)
                {
                    profferCommands3.RemoveNamedCommand(name);
                }
            }
            catch (Exception)
            {
            }

            string[] bars =
            {
                "Project",
                "Item",
                "Easy MDI Document Window",
                "Cross Project Multi Item",
                "Cross Project Multi Project",
            };

            const string Prefix = "Aurora.NiftyPerforce.Connect";
            string absname = Prefix + "." + name;

            foreach (string bar in bars)
            {
                CommandBar? b = (_plugin?.App.CommandBars as CommandBars)?[bar];
                if (b != null)
                {
                    bool done = false;
                    while (!done)
                    {
                        bool found = false;
                        foreach (CommandBarControl ctrl in b.Controls)
                        {
                            if (ctrl.Caption == name || ctrl.Caption == absname)
                            {
                                found = true;
                                try
                                {
                                    profferCommands3.RemoveCommandBarControl(ctrl);
                                }
                                catch (Exception)
                                {
                                }

                                break;
                            }
                        }

                        done = !found;
                    }
                }
            }
        }

        // Remove a command bar and contained controls
        private static void RemoveCommandBar(string name, IVsProfferCommands3 profferCommands3)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = GetGlobalService(typeof(DTE)) as DTE2;
            var commandBars = dte?.CommandBars as CommandBars;
            CommandBar? existingCmdBar = null;

            try
            {
                existingCmdBar = commandBars?[name];
            }
            catch (Exception)
            {
            }

            if (existingCmdBar != null)
            {
                // Remove all buttons
                while (existingCmdBar.Controls.Count > 0)
                {
                    foreach (CommandBarControl ctrl in existingCmdBar.Controls)
                    {
                        profferCommands3.RemoveCommandBarControl(ctrl);
                        break;
                    }
                }
            }

            profferCommands3.RemoveCommandBar(existingCmdBar);
        }
    }
}
