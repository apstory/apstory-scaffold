﻿using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Apstory.Scaffold.VisualStudio
{

    public sealed partial class ApstoryScaffoldVisualStudioPackage : AsyncPackage
    {
        private readonly string scaffoldApp = "Apstory.Scaffold.App";
        private string scaffoldAppLocation = string.Empty;

        private string ExecuteCmd(string commandName, string arguments)
        {
            string output = string.Empty;
            string error = string.Empty;

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = commandName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = psi })
            {
                process.Start();

                // Read standard output and standard error synchronously
                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();

                process.WaitForExit(); // Wait for the process to exit
            }

            return output ?? error;
        }

        /// <summary>
        /// Execute Apstory Scaffolding Command
        /// </summary>
        /// <param name="tableOrStoredProcName"> dbo.tableName // dbo.zgen_table_command </param>
        /// <param name="workingDirectory">Directory to execute the command in</param>
        private async Task<List<string>> ExecuteScaffolding(string tableOrStoredProcName, string workingDirectory)
        {
            string arguments = $"-regen {tableOrStoredProcName}";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = scaffoldAppLocation,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            List<string> logs = new List<string>();
            using (Process process = new Process { StartInfo = psi })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        logs.Add(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        logs.Add($"Error: {e.Data}");
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();
            }

            return logs;
        }

        private async void Log(string message)
        {
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

        private string GetConfigPath()
        {
            string solutionPath = GetSolutionDirectory();
            if (solutionPath is null)
            {
                Log("GetConfigPath Failed: No Solution found");
                return string.Empty;
            }

            string configPath = Path.Combine(solutionPath, ".vs", "apstory-scaffold-settings.json");
            return configPath;
        }

        private string GetSolutionDirectory()
        {
            ThreadHelper.ThrowIfNotOnUIThread(); // Ensure we are on the main thread

            IVsSolution solutionService = ServiceProvider.GlobalProvider.GetService(typeof(SVsSolution)) as IVsSolution;
            if (solutionService == null)
                return null;

            solutionService.GetSolutionInfo(out string solutionDirectory, out _, out _);
            return solutionDirectory;
        }

        private string GetActiveDocumentPath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsMonitorSelection monitorSelection = ServiceProvider.GlobalProvider.GetService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
            if (monitorSelection == null)
                return null;

            monitorSelection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out object frame);
            if (frame is IVsWindowFrame windowFrame)
            {
                windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out object docPath);
                return docPath as string;
            }

            return null;
        }

        private string GetSchemaFromPath(string path)
        {
            string directory = Path.GetDirectoryName(path);

            if (directory == null)
                throw new ArgumentException("Invalid path provided.");

            // Get the parent directory (schema folder)
            string schema = Directory.GetParent(directory)?.Name;

            if (string.IsNullOrEmpty(schema))
                throw new InvalidOperationException("Schema folder not found in the provided path.");

            return schema;
        }


        //EnvDTE is being phased out in later versions of visual studio
        [Obsolete]
        private string GetActiveFileContent()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            EnvDTE.DTE app = (EnvDTE.DTE)GetService(typeof(SDTE));
            if (app.ActiveDocument != null && app.ActiveDocument.Type == "Text")
            {
                EnvDTE.TextDocument textDoc = (EnvDTE.TextDocument)app.ActiveDocument.Object("TextDocument");

                var editPoint = textDoc.StartPoint.CreateEditPoint();
                var documentText = editPoint.GetText(textDoc.EndPoint.CreateEditPoint());

                return documentText;
            }

            return string.Empty;
        }

        private List<string> GetSelectedItemPaths()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            List<string> selectedPaths = new List<string>();

            var dte = (EnvDTE.DTE)ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE));
            if (dte == null) return selectedPaths;

            var selectedObjects = dte.SelectedItems as EnvDTE.SelectedItems;
            if (selectedObjects != null)
            {
                foreach (EnvDTE.SelectedItem selectedItem in selectedObjects)
                {
                    if (selectedItem.ProjectItem != null)
                    {
                        string fullPath = selectedItem.ProjectItem.FileNames[1]; // Index 1 gives the full path
                        if (!string.IsNullOrEmpty(fullPath))
                        {
                            selectedPaths.Add(fullPath);
                        }
                    }
                }
            }

            return selectedPaths;
        }

    }
}
