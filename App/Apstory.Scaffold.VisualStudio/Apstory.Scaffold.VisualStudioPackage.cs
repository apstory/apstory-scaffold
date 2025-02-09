using Apstory.Scaffold.VisualStudio.Window;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
    public sealed class ApstoryScaffoldVisualStudioPackage : AsyncPackage
    {
        /// <summary>
        /// Apstory.Scaffold.VisualStudioPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "033376d0-4630-4068-89d7-e6629cfe6645";

        private const string guidApstoryScaffoldVisualStudioPackageCmdSet = "bf130a03-5202-4448-8173-02f6b1d00bd2"; // Match `guidApstoryScaffoldVisualStudioPackageCmdSet`
        private const int ToolbarTestCommandId = 0x1051; // Matches `ToolbarTestCommandId`

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
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            OleMenuCommandService commandService = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var cmdId = new CommandID(new Guid(guidApstoryScaffoldVisualStudioPackageCmdSet), ToolbarTestCommandId);
                var menuItem = new MenuCommand(ExecuteCommand, cmdId);
                commandService.AddCommand(menuItem);
            }
        }

        private void ExecuteCommand(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            WriteToOutputWindow("Dude, you executed something");

            GetActiveFileContent();
            //VsShellUtilities.ShowMessageBox(
            //    this,
            //    "Scaffold Code Button Clicked!",
            //    "Information",
            //    OLEMSGICON.OLEMSGICON_INFO,
            //    OLEMSGBUTTON.OLEMSGBUTTON_OK,
            //    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        private void GetActiveFileContent()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the active text view
            var textManager = (IVsTextManager)this.GetService(typeof(SVsTextManager));
            if (textManager == null) return;

            textManager.GetActiveView(1, null, out IVsTextView textView);

            if (textView != null)
            {
                var path = GetFilePathFromTextView(textView);
                WriteToOutputWindow($"Current open view file: {path}");

                // Get the buffer associated with the text view
                textView.GetBuffer(out IVsTextLines textLines);

                if (textLines != null)
                {
                    // Get the number of lines in the buffer
                    textLines.GetLineCount(out int lineCount);

                    WriteToOutputWindow($"Lines: {lineCount}");

                    StringBuilder fileContent = new StringBuilder();

                    // Iterate through each line and get the text
                    for (int lineNum = 0; lineNum < lineCount; lineNum++)
                    {
                        textLines.GetLineText(lineNum, 0, lineNum, int.MaxValue, out string lineText);
                        fileContent.AppendLine(lineText);
                    }

                    // Write the content of the file to the Output window
                    WriteToOutputWindow($"Currently open file content: {fileContent}");
                }
            }
        }

        private void WriteToOutputWindow(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the Output window service
            var outputWindow = (IVsOutputWindow)this.GetService(typeof(SVsOutputWindow));
            if (outputWindow == null)
                return;

            // Create a new pane or get the existing one
            Guid generalPaneGuid = VSConstants.GUID_OutWindowGeneralPane;
            outputWindow.GetPane(ref generalPaneGuid, out IVsOutputWindowPane outputPane);

            if (outputPane == null)
            {
                outputWindow.CreatePane(ref generalPaneGuid, "Apstory Scaffold", 1, 1);
                outputWindow.GetPane(ref generalPaneGuid, out outputPane);
            }

            // Write message to the pane
            outputPane.OutputString(message + "\n");
        }

        private string GetFilePathFromTextView(IVsTextView textView)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the IVsRunningDocumentTable service
            var rdt = (IVsRunningDocumentTable)this.GetService(typeof(SVsRunningDocumentTable));
            if (rdt == null)
            {
                return "Unable to retrieve running document table";
            }

            uint docCookie = 0;
            string filePath = null;
            IVsHierarchy hierarchy = null;
            uint itemId = 0;
            uint rdtFlags = 0;
            uint readOnly = 0;
            uint editlock = 0;
            IntPtr docData = IntPtr.Zero;

            // GetDocumentInfo requires a docCookie to be filled
            int result = rdt.GetDocumentInfo(
                docCookie,
                out rdtFlags,
                out readOnly,
                out editlock,
                out filePath,
                out hierarchy,
                out itemId,
                out docData);

            // Debugging output
            if (result != VSConstants.S_OK)
            {
                return $"Error retrieving document info. Result: {result} ({result.ToString("X8")})";
            }

            if (string.IsNullOrEmpty(filePath))
            {
                return "File path is empty or null";
            }

            return filePath;
        }


        #endregion
    }
}
