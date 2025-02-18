﻿using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Apstory.Scaffold.VisualStudio
{

    public sealed partial class ApstoryScaffoldVisualStudioPackage : AsyncPackage
    {
        private bool isScaffolding = false;

        private async void ExecuteToolbarOpenConfigAsync(object sender, EventArgs e)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var configPath = GetConfigPath();
                if (string.IsNullOrEmpty(configPath))
                    return;

                DTE dte = await ServiceProvider.GetGlobalServiceAsync(typeof(DTE)) as DTE;
                dte.ItemOperations.OpenFile(configPath, EnvDTE.Constants.vsViewKindTextView);
            }
            catch (Exception ex)
            {
                Log($"Error in ExecuteToolbarOpenConfigAsync: {ex.Message}");
            }
        }

        private async void ExecuteToolbarSqlUpdateAsync(object sender, EventArgs e)
        {
            await this.LoadConfigAsync();
            if (string.IsNullOrEmpty(this.config.SqlDestination))
            {
                Log("SQL Destination Required");
                ExecuteToolbarOpenConfigAsync(sender, e);
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var solutionDirectory = GetSolutionDirectory();

            Log($"Executing Code Scaffold in {solutionDirectory} using {this.config.SqlDestination}");

            // Run the process on a background thread
            var logs = await Task.Run(() => ExecuteSqlUpdate(string.Empty, solutionDirectory));

            // Return to the UI thread for logging
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            foreach (var log in logs)
                Log(log);

            Log($"SQL Update Complete.");
        }

        private async void ExecuteToolbarCodeScaffoldAsync(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (isScaffolding)
            {
                Log("Scaffolding is already running. Please wait.");
                return;
            }

            isScaffolding = true;
            btnRunCodeScaffold.Enabled = !isScaffolding;

            try
            {
                var solutionDirectory = GetSolutionDirectory();
                var activeDocPath = GetActiveDocumentPath();
                var fileName = Path.GetFileName(activeDocPath);
                if (!fileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                {
                    Log($"Scaffolding only supports .sql files");
                    return;
                }

                Log($"Executing Code Scaffold for {fileName} in {solutionDirectory}");
                var schema = GetSchemaFromPath(activeDocPath);
                var tableOrProc = fileName.Replace(".sql", string.Empty);

                // Run the process on a background thread
                var logs = await Task.Run(() => ExecuteScaffolding($"{schema}.{tableOrProc}", solutionDirectory));

                // Return to the UI thread for logging
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                foreach (var log in logs)
                    Log(log);

                Log($"Scaffolding completed for {fileName}");
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                Log($"Error in ExecuteToolbarCodeScaffoldAsync: {ex.Message}");
            }
            finally
            {
                isScaffolding = false;
                btnRunCodeScaffold.Enabled = !isScaffolding;
            }
        }

        private async void ExecuteContextMenuSqlUpdateAsync(object sender, EventArgs e)
        {
            await this.LoadConfigAsync();
            if (string.IsNullOrEmpty(this.config.SqlDestination))
            {
                Log("SQL Destination Required");
                ExecuteToolbarOpenConfigAsync(sender, e);
                return;
            }

            var solutionDirectory = GetSolutionDirectory();
            var selectedPaths = GetSelectedItemPaths();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            List<string> toUpdate = new List<string>();
            foreach (var path in selectedPaths)
            {
                var fileName = Path.GetFileName(path);
                if (!fileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                {
                    Log($"Scaffolding only supports .sql files");
                    return;
                }

                var schema = GetSchemaFromPath(path);
                var tableOrProc = fileName.Replace(".sql", string.Empty);
                toUpdate.Add($"{schema}.{tableOrProc}");
            }

            var scaffoldArgs = string.Join(";", toUpdate.ToArray());
            Log($"Executing Code Scaffold for {scaffoldArgs} in {solutionDirectory} using {this.config.SqlDestination}");

            // Run the process on a background thread
            var logs = await Task.Run(() => ExecuteSqlUpdate(scaffoldArgs, solutionDirectory));

            // Return to the UI thread for logging
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            foreach (var log in logs)
                Log(log);

            Log($"SQL Update Complete.");
        }

        private async void ExecuteContextMenuCodeScaffoldAsync(object sender, EventArgs e)
        {
            var solutionDirectory = GetSolutionDirectory();
            var selectedPaths = GetSelectedItemPaths();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (isScaffolding)
            {
                Log("Scaffolding is already running. Please wait.");
                return;
            }

            isScaffolding = true;
            btnRunCodeScaffold.Enabled = !isScaffolding;

            try
            {

                List<string> toScaffold = new List<string>();
                foreach (var path in selectedPaths)
                {
                    var fileName = Path.GetFileName(path);
                    if (!fileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                    {
                        Log($"Scaffolding only supports .sql files");
                        return;
                    }

                    var schema = GetSchemaFromPath(path);
                    var tableOrProc = fileName.Replace(".sql", string.Empty);
                    toScaffold.Add($"{schema}.{tableOrProc}");
                }

                var scaffoldArgs = string.Join(";", toScaffold.ToArray());
                Log($"Executing Code Scaffold for {scaffoldArgs} in {solutionDirectory}");

                // Run the process on a background thread
                var logs = await Task.Run(() => ExecuteScaffolding(scaffoldArgs, solutionDirectory));

                // Return to the UI thread for logging
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                foreach (var log in logs)
                    Log(log);

                Log($"Scaffolding Complete.");
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                Log($"Error in ExecuteContextMenuCodeScaffoldAsync: {ex.Message}");
            }
            finally
            {
                isScaffolding = false;
                btnRunCodeScaffold.Enabled = !isScaffolding;
            }
        }
    }
}
