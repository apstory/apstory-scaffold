using Apstory.Scaffold.VisualStudio.Model;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Apstory.Scaffold.VisualStudio
{

    public sealed partial class ApstoryScaffoldVisualStudioPackage : AsyncPackage
    {
        private bool isScaffolding = false;
        private bool isSqlPushing = false;
        private bool isSqlDeleting = false;

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
                LogError("SQL Destination Required", Hardcoded.ErrorLogSql);
                ExecuteToolbarOpenConfigAsync(sender, e);
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (isSqlPushing)
            {
                Log("SQL push is already running. Please wait.");
                return;
            }

            isSqlPushing = true;
            btnSqlUpdate.Enabled = !isSqlPushing;

            try
            {
                var solutionDirectory = GetSolutionDirectory();
                Log($"Executing Code Scaffold in {solutionDirectory} using {this.config.SqlDestination}");

                // Run the process on a background thread
                var logs = await Task.Run(() => ExecuteSqlUpdate(string.Empty, solutionDirectory));
                // Return to the UI thread for logging
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                foreach (var log in logs)
                    Log(log);

                List<string> errorsToLog = logs.Where(s => s.StartsWith("[Error]")).ToList();
                if (errorsToLog.Any())
                {
                    ErrorListProvider.Tasks.Clear();

                    foreach (var errorMessage in errorsToLog)
                        LogError(errorMessage, Hardcoded.ErrorLogSql);

                    ErrorListProvider.Show(); // This ensures the Error List pops up
                }

                Log($"SQL Update Complete.");
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                LogError($"Exception in ExecuteToolbarSqlUpdateAsync: {ex.Message}");
                ErrorListProvider.Show();
            }
            finally
            {
                isSqlPushing = false;
                btnSqlUpdate.Enabled = !isSqlPushing;
            }
        }

        private async void ExecuteToolbarCodeScaffoldAsync(object sender, EventArgs e)
        {
            await this.LoadConfigAsync();
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
                var activeDocPath = GetActiveDocumentPath();
                var workingDirectory = FindClosestSolutionFolder(activeDocPath);
                var fileName = Path.GetFileName(activeDocPath);
                if (!fileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                {
                    Log($"Scaffolding only supports .sql files");
                    return;
                }

                Log($"Executing Code Scaffold for {fileName} in {workingDirectory}");
                var schema = GetSchemaFromPath(activeDocPath);
                var tableOrProc = fileName.Replace(".sql", string.Empty);

                // Run the process on a background thread
                var logs = await Task.Run(() => ExecuteScaffolding($"{schema}.{tableOrProc}", workingDirectory));

                // Return to the UI thread for logging
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                foreach (var log in logs)
                    Log(log);

                List<string> errorsToLog = logs.Where(s => s.StartsWith("[Error]")).ToList();
                if (errorsToLog.Any())
                {
                    ErrorListProvider.Tasks.Clear();

                    foreach (var errorMessage in errorsToLog)
                        LogError(errorMessage, Hardcoded.ErrorLogScaffold);

                    ErrorListProvider.Show(); // This ensures the Error List pops up
                }

                Log($"Scaffolding completed for {fileName}");
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                LogError($"Exception in ExecuteToolbarCodeScaffoldAsync: {ex.Message}");
                ErrorListProvider.Show();
            }
            finally
            {
                isScaffolding = false;
                btnRunCodeScaffold.Enabled = !isScaffolding;
            }
        }

        private async void ExecuteToolbarSqlDeleteAsync(object sender, EventArgs e)
        {
            await this.LoadConfigAsync();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (isSqlDeleting)
            {
                Log("Delete operation is already running. Please wait.");
                return;
            }

            isSqlDeleting = true;
            btnSqlDelete.Enabled = !isSqlDeleting;

            try
            {
                var activeDocPath = GetActiveDocumentPath();
                var workingDirectory = FindClosestSolutionFolder(activeDocPath);
                var fileName = Path.GetFileName(activeDocPath);
                if (!fileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                {
                    Log($"Deleting only supports .sql files");
                    return;
                }

                Log($"Executing Deletion for {fileName} in {workingDirectory}");
                var schema = GetSchemaFromPath(activeDocPath);
                var tableOrProc = fileName.Replace(".sql", string.Empty);

                // Run the process on a background thread
                var logs = await Task.Run(() => ExecuteSQLDelete($"{schema}.{tableOrProc}", workingDirectory));

                // Return to the UI thread for logging
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                foreach (var log in logs)
                    Log(log);

                List<string> errorsToLog = logs.Where(s => s.StartsWith("[Error]")).ToList();
                if (errorsToLog.Any())
                {
                    ErrorListProvider.Tasks.Clear();

                    foreach (var errorMessage in errorsToLog)
                        LogError(errorMessage, Hardcoded.ErrorLogScaffold);

                    ErrorListProvider.Show(); // This ensures the Error List pops up
                }

                Log($"Deletion completed for {fileName}");
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                LogError($"Exception in ExecuteToolbarSqlDeleteAsync: {ex.Message}");
                ErrorListProvider.Show();
            }
            finally
            {
                isSqlDeleting = false;
                btnSqlDelete.Enabled = !isSqlDeleting;
            }
        }

        private async void ExecuteContextMenuSqlUpdateAsync(object sender, EventArgs e)
        {
            await this.LoadConfigAsync();
            if (string.IsNullOrEmpty(this.config.SqlDestination))
            {
                LogError("SQL Destination Required", Hardcoded.ErrorLogSql);
                ExecuteToolbarOpenConfigAsync(sender, e);
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (isSqlPushing)
            {
                Log("SQL Push is already running. Please wait.");
                return;
            }

            isSqlPushing = true;
            btnSqlUpdate.Enabled = !isSqlPushing;

            try
            {
                var selectedPaths = GetSelectedItemPaths();
                var workingDirectory = FindClosestSolutionFolder(selectedPaths.First());
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
                Log($"Executing Code Scaffold for {scaffoldArgs} in {workingDirectory} using {this.config.SqlDestination}");

                // Run the process on a background thread
                var logs = await Task.Run(() => ExecuteSqlUpdate(scaffoldArgs, workingDirectory));

                // Return to the UI thread for logging
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                foreach (var log in logs)
                    Log(log);

                List<string> errorsToLog = logs.Where(s => s.StartsWith("[Error]")).ToList();
                if (errorsToLog.Any())
                {
                    ErrorListProvider.Tasks.Clear();

                    foreach (var errorMessage in errorsToLog)
                        LogError(errorMessage, Hardcoded.ErrorLogSql);

                    ErrorListProvider.Show(); // This ensures the Error List pops up
                }

                Log($"SQL Update Complete.");
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                LogError($"Exception in ExecuteContextMenuSqlUpdateAsync: {ex.Message}");
                ErrorListProvider.Show();
            }
            finally
            {
                isSqlPushing = false;
                btnSqlUpdate.Enabled = !isSqlPushing;
            }
        }

        private async void ExecuteContextMenuCodeScaffoldAsync(object sender, EventArgs e)
        {
            await this.LoadConfigAsync();
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
                var workingDirectory = FindClosestSolutionFolder(selectedPaths.First());
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
                Log($"Executing Code Scaffold for {scaffoldArgs} in {workingDirectory}");

                // Run the process on a background thread
                var logs = await Task.Run(() => ExecuteScaffolding(scaffoldArgs, workingDirectory));

                // Return to the UI thread for logging
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                foreach (var log in logs)
                    Log(log);

                List<string> errorsToLog = logs.Where(s => s.StartsWith("[Error]")).ToList();
                if (errorsToLog.Any())
                {
                    ErrorListProvider.Tasks.Clear();

                    foreach (var errorMessage in errorsToLog)
                        LogError(errorMessage, Hardcoded.ErrorLogScaffold);

                    ErrorListProvider.Show(); // This ensures the Error List pops up
                }

                Log($"Scaffolding Complete.");
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                LogError($"Exception in ExecuteContextMenuCodeScaffoldAsync: {ex.Message}");
                ErrorListProvider.Show();
            }
            finally
            {
                isScaffolding = false;
                btnRunCodeScaffold.Enabled = !isScaffolding;
            }
        }

        private async void ExecuteContextMenuSqlDeleteAsync(object sender, EventArgs e)
        {
            await this.LoadConfigAsync();
            var selectedPaths = GetSelectedItemPaths();
            var workingDirectory = FindClosestSolutionFolder(selectedPaths.First());

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (isSqlDeleting)
            {
                Log("Delete operation is already running. Please wait.");
                return;
            }

            isSqlDeleting = true;
            btnSqlDelete.Enabled = !isSqlDeleting;

            try
            {
                List<string> toDelete = new List<string>();
                foreach (var path in selectedPaths)
                {
                    var fileName = Path.GetFileName(path);
                    if (!fileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                    {
                        Log($"Deleting only supports .sql files");
                        return;
                    }

                    var schema = GetSchemaFromPath(path);
                    var tableOrProc = fileName.Replace(".sql", string.Empty);
                    toDelete.Add($"{schema}.{tableOrProc}");
                }

                var scaffoldArgs = string.Join(";", toDelete.ToArray());
                Log($"Executing Deletion for {scaffoldArgs} in {workingDirectory}");

                // Run the process on a background thread
                var logs = await Task.Run(() => ExecuteSQLDelete(scaffoldArgs, workingDirectory));

                // Return to the UI thread for logging
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                foreach (var log in logs)
                    Log(log);

                List<string> errorsToLog = logs.Where(s => s.StartsWith("[Error]")).ToList();
                if (errorsToLog.Any())
                {
                    ErrorListProvider.Tasks.Clear();

                    foreach (var errorMessage in errorsToLog)
                        LogError(errorMessage, Hardcoded.ErrorLogScaffold);

                    ErrorListProvider.Show(); // This ensures the Error List pops up
                }

                Log($"Deletion Complete.");
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                LogError($"Exception in ExecuteContextMenuSqlDeleteAsync: {ex.Message}");
                ErrorListProvider.Show();
            }
            finally
            {
                isSqlDeleting = false;
                btnSqlDelete.Enabled = !isSqlDeleting;
            }
        }

        private string FindClosestSolutionFolder(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("SQL file path cannot be null or empty.", nameof(filePath));

            var directory = new DirectoryInfo(Path.GetDirectoryName(filePath));

            while (directory != null)
            {
                var slnFiles = directory.GetFiles("*.sln");
                if (slnFiles.Length > 0)
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            // Fallback to the root directory if no solution folder is found
            return GetSolutionDirectory();
        }
    }
}
