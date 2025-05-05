using Apstory.Scaffold.VisualStudio.Model;
using Apstory.Scaffold.VisualStudio.Window;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.TextTemplating.VSHost;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Task = System.Threading.Tasks.Task;

namespace Apstory.Scaffold.VisualStudio
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
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(ApstoryScaffoldVisualStudioPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(Apstory.Scaffold.VisualStudio.Window.ScaffoldWindow))]
    public sealed partial class ApstoryScaffoldVisualStudioPackage : AsyncPackage
    {
        /// <summary>
        /// Apstory.Scaffold.VisualStudioPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "033376d0-4630-4068-89d7-e6629cfe6645";

        private const string guidApstoryScaffoldVisualStudioPackageCmdSet = "cf130a03-5202-4448-8173-02f6b1d00bd2"; // Match `guidApstoryScaffoldVisualStudioPackageCmdSet`
        
        private const int ToolbarApstoryScaffoldCommandId = 0x1051; //Toolbar Run Scaffold
        private const int ToolbarApstorySqlUpdateCommandId = 0x1055;   
        private const int ToolbarApstoryConfigCommandId = 0x1053;   
        private const int ToolbarApstoryDeleteCommandId = 0x1056;   

        private const int ContextMenuScaffoldCommandId = 0x1052;    
        private const int ContextMenuSqlUpdateCommandId = 0x1054;
        private const int ContextMenuSqlDeleteCommandId = 0x1057;   


        private MenuCommand btnRunCodeScaffold;
        private MenuCommand btnOpenConfig;
        private MenuCommand btnSqlUpdate;
        private MenuCommand btnSqlDelete;

        private ScaffoldConfig config;

        private ErrorListProvider _errorListProvider;
        public ErrorListProvider ErrorListProvider
        {
            get
            {
                if (_errorListProvider == null)
                    _errorListProvider = new ErrorListProvider(ServiceProvider.GlobalProvider);

                return _errorListProvider;
            }
        }
        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            OleMenuCommandService commandService = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                //Toolbar Buttons
                var cmdToolbarRunScaffoldId = new CommandID(new Guid(guidApstoryScaffoldVisualStudioPackageCmdSet), ToolbarApstoryScaffoldCommandId);
                btnRunCodeScaffold = new MenuCommand(ExecuteToolbarCodeScaffoldAsync, cmdToolbarRunScaffoldId);
                commandService.AddCommand(btnRunCodeScaffold);

                var cmdToolbarSqlUpdateId = new CommandID(new Guid(guidApstoryScaffoldVisualStudioPackageCmdSet), ToolbarApstorySqlUpdateCommandId);
                btnSqlUpdate = new MenuCommand(ExecuteToolbarSqlUpdateAsync, cmdToolbarSqlUpdateId);
                commandService.AddCommand(btnSqlUpdate);

                var cmdToolbarOpenSettingsId = new CommandID(new Guid(guidApstoryScaffoldVisualStudioPackageCmdSet), ToolbarApstoryConfigCommandId);
                btnOpenConfig = new MenuCommand(ExecuteToolbarOpenConfigAsync, cmdToolbarOpenSettingsId);
                commandService.AddCommand(btnOpenConfig);

                var cmdToolbarSqlDeleteId = new CommandID(new Guid(guidApstoryScaffoldVisualStudioPackageCmdSet), ToolbarApstoryDeleteCommandId);
                btnSqlDelete = new MenuCommand(ExecuteToolbarSqlDeleteAsync, cmdToolbarSqlDeleteId);
                commandService.AddCommand(btnSqlDelete);


                //Right-click Context Menu Buttons
                var cmdContextCodeScaffold = new CommandID(new Guid(guidApstoryScaffoldVisualStudioPackageCmdSet), ContextMenuScaffoldCommandId);
                var menuScaffoldCommand = new OleMenuCommand(ExecuteContextMenuCodeScaffoldAsync, cmdContextCodeScaffold);
                commandService?.AddCommand(menuScaffoldCommand);

                var cmdContextSqlUpdate = new CommandID(new Guid(guidApstoryScaffoldVisualStudioPackageCmdSet), ContextMenuSqlUpdateCommandId);
                var menuSqlUpdateCommand = new OleMenuCommand(ExecuteContextMenuSqlUpdateAsync, cmdContextSqlUpdate);
                commandService?.AddCommand(menuSqlUpdateCommand);

                var cmdContextSqlDelete = new CommandID(new Guid(guidApstoryScaffoldVisualStudioPackageCmdSet), ContextMenuSqlDeleteCommandId);
                var menuSqlDeleteCommand = new OleMenuCommand(ExecuteContextMenuSqlDeleteAsync, cmdContextSqlDelete);
                commandService?.AddCommand(menuSqlDeleteCommand);
            }


            this.scaffoldAppLocation = ExecuteCmd("where.exe", scaffoldApp).Trim('\r', '\n', ' ');
            Log($"Found Scaffold App at {this.scaffoldAppLocation}");

            await LoadConfigAsync();
        }

        
        #endregion
    }
}
