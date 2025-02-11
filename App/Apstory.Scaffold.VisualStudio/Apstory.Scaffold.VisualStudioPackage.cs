using Apstory.Scaffold.VisualStudio.Window;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.TextTemplating.VSHost;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading;
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

        private const string guidApstoryScaffoldVisualStudioPackageCmdSet = "bf130a03-5202-4448-8173-02f6b1d00bd2"; // Match `guidApstoryScaffoldVisualStudioPackageCmdSet`
        private const int ToolbarApstoryScaffoldCommandId = 0x1051;
        private const int ContextMenuScaffoldCommandId = 0x1052;

        private MenuCommand btnRunCodeScaffold;
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
                //Add toolbar button
                var cmdId = new CommandID(new Guid(guidApstoryScaffoldVisualStudioPackageCmdSet), ToolbarApstoryScaffoldCommandId);
                btnRunCodeScaffold = new MenuCommand(ExecuteToolbarCodeScaffold, cmdId);
                commandService.AddCommand(btnRunCodeScaffold);

                //Add right-click context menu button
                var cmdContextCodeScaffold = new CommandID(new Guid(guidApstoryScaffoldVisualStudioPackageCmdSet), ContextMenuScaffoldCommandId);
                var menuCommand = new OleMenuCommand(ExecuteContextMenuCodeScaffold, cmdContextCodeScaffold);
                commandService?.AddCommand(menuCommand);
            }


            this.scaffoldAppLocation = ExecuteCmd("where.exe", scaffoldApp).Trim('\r', '\n', ' ');
            Log($"Found Scaffold App at {this.scaffoldAppLocation}");
        }


        #endregion
    }
}
