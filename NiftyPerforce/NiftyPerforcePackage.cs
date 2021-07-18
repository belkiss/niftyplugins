// Copyright (C) 2006-2015 Jim Tilander. See COPYING for and README for more details.
using System;
using System.ComponentModel.Design;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Aurora;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.CommandBars;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

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
    // Register the product to be listed in About box
    [InstalledProductRegistration("#110", "#112", Vsix.Version, IconResourceID = 400)]
    [ProvideAutoLoad(Microsoft.VisualStudio.VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)] // Note: the package must be loaded on startup to create and bind commands
                                                                                                                           // Register the resource ID of the CTMENU section (generated from compiling the VSCT file), so the IDE will know how to merge this package's menus with the rest of the IDE when "devenv /setup" is run
                                                                                                                           // The menu resource ID needs to match the ResourceName number defined in the csproj project file in the VSCTCompile section
                                                                                                                           // Everytime the version number changes VS will automatically update the menus on startup; if the version doesn't change, you will need to run manually "devenv /setup /rootsuffix:Exp" to see VSCT changes reflected in IDE
    [ProvideMenuResource("Menus.ctmenu", 1)]
    // Register a sample options page visible as Tools/Options/SourceControl/NiftyPerforceSettings when the provider is active
    [ProvideOptionPage(typeof(Config), "Source Control", Vsix.Name, 106, 107, false)]
    //[ProvideToolsOptionsPageVisibility("Source Control", "Nifty Perforce Settings", PackageGuids.guidNiftyPerforceSccProviderString)]
    // Declare the package guid
    [Guid(PackageGuids.guidNiftyPerforcePackageString)]
    public sealed class NiftyPerforcePackage : AsyncPackage
    {
        private Plugin m_plugin = null;
        private CommandRegistry m_commandRegistry = null;

        public NiftyPerforcePackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            // Every plugin needs a command bar.
            var application = await GetServiceAsync(typeof(DTE)).ConfigureAwait(false) as DTE2;
            var oleMenuCommandService = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;

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

#if DEBUG
            Log.Info("NiftyPerforce (Debug)");
#else
            Log.Info("NiftyPerforce (Release)");
#endif

            // Show where we are and when we were compiled...
            var niftyAssembly = Assembly.GetExecutingAssembly();
            Version version = niftyAssembly.GetName().Version;
            string versionString = string.Join(".", version.Major, version.Minor, version.Build);
            string informationalVersion = niftyAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (informationalVersion != null)
                versionString += " " + informationalVersion;

            Log.Info("I'm running {0} v{1} compiled on {2}", niftyAssembly.Location, versionString, System.IO.File.GetLastWriteTime(niftyAssembly.Location));

            // Now we can take care of registering ourselves and all our commands and hooks.
            Log.Debug("Booting up...");
            Log.IncIndent();

            var config = (Config)GetDialogPage(typeof(Config));
            Singleton<Config>.Instance = config;

            config.OnApplyEvent += (object sender, EventArgs e) =>
            {
                if (config.CleanLegacyNiftyCommands)
                {
                    Cleanup();
                    config.CleanLegacyNiftyCommands = false;
                }
            };

            m_plugin = new Plugin(application, oleMenuCommandService, config);

            m_commandRegistry = new CommandRegistry(m_plugin, new Guid(PackageGuids.guidNiftyPerforcePackageCmdSetString));

            // Add our command handlers for menu (commands must exist in the .vsct file)
            m_commandRegistry.RegisterCommand(new P4EditModified(m_plugin, "NiftyEditModified"));
            m_commandRegistry.RegisterCommand(new P4EditItem(m_plugin, "NiftyEdit"));
            m_commandRegistry.RegisterCommand(new P4DiffItem(m_plugin, "NiftyDiff"));
            m_commandRegistry.RegisterCommand(new P4RevisionHistoryItem(m_plugin, "NiftyHistory", false));
            m_commandRegistry.RegisterCommand(new P4RevisionHistoryItem(m_plugin, "NiftyHistoryMain", true));
            m_commandRegistry.RegisterCommand(new P4TimeLapseItem(m_plugin, "NiftyTimeLapse", false));
            m_commandRegistry.RegisterCommand(new P4TimeLapseItem(m_plugin, "NiftyTimeLapseMain", true));
            m_commandRegistry.RegisterCommand(new P4RevisionGraphItem(m_plugin, "NiftyRevisionGraph", false));
            m_commandRegistry.RegisterCommand(new P4RevisionGraphItem(m_plugin, "NiftyRevisionGraphMain", true));
            m_commandRegistry.RegisterCommand(new P4RevertItem(m_plugin, "NiftyRevert", false));
            m_commandRegistry.RegisterCommand(new P4RevertItem(m_plugin, "NiftyRevertUnchanged", true));
            m_commandRegistry.RegisterCommand(new P4ShowItem(m_plugin, "NiftyShow"));

            m_plugin.AddFeature(new AutoAddDelete(m_plugin, this));
            m_plugin.AddFeature(new AutoCheckoutProject(m_plugin));
            m_plugin.AddFeature(new AutoCheckoutTextEdit(m_plugin));
            m_plugin.AddFeature(new AutoCheckoutOnSave(m_plugin, this));

            P4Operations.CheckInstalledFiles();

            AsyncProcess.Init();

            Log.DecIndent();
            Log.Debug("Initialized...");
        }

        /// <summary>
        ///  Removes all installed legacy commands and controls.
        /// </summary>
        public void Cleanup()
        {
            Log.Info("Cleaning up all legacy nifty commands");

            ThreadHelper.ThrowIfNotOnUIThread();

            var profferCommands3 = base.GetService(typeof(SVsProfferCommands)) as IVsProfferCommands3;
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
                Command cmd = m_plugin.Commands.Item(name, -1);
                if (cmd != null)
                {
                    profferCommands3.RemoveNamedCommand(name);
                }
            }
            catch (Exception) { }

            string[] bars = {
                "Project",
                "Item",
                "Easy MDI Document Window",
                "Cross Project Multi Item",
                "Cross Project Multi Project"
            };

            const string prefix = "Aurora.NiftyPerforce.Connect";
            string Absname = prefix + "." + name;

            foreach (string bar in bars)
            {
                CommandBar b = ((CommandBars)m_plugin.App.CommandBars)[bar];
                if (null != b)
                {
                    bool done = false;
                    while (!done)
                    {
                        bool found = false;
                        foreach (CommandBarControl ctrl in b.Controls)
                        {
                            if (ctrl.Caption == name || ctrl.Caption == Absname)
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
            var commandBars = (CommandBars)dte.CommandBars;
            CommandBar existingCmdBar = null;

            try
            {
                existingCmdBar = commandBars[name];
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

        #endregion
    }
}
