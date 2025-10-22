// Import necessary modules
import * as vscode from 'vscode';
import * as path from 'path';

/**
 * Handles SQL related commands
 */
export function registerSqlCommands(context: vscode.ExtensionContext): void {
    // Register Run Code Scaffold command
    const runCodeScaffold = vscode.commands.registerCommand('apstoryScaffold.runCodeScaffold', async (uri, selectedFiles) => {
        await handleRunCodeScaffold(uri, selectedFiles);
    });
    
    // Register Push SQL Changes command
    const pushSqlChanges = vscode.commands.registerCommand('apstoryScaffold.pushSqlChanges', (uri) => {
        if (validateSqlFile(uri)) {
            vscode.window.showInformationMessage('Push SQL Changes command executed.');
        }
    });
    
    // Register Delete command
    const deleteCmd = vscode.commands.registerCommand('apstoryScaffold.delete', (uri) => {
        if (validateSqlFile(uri)) {
            vscode.window.showInformationMessage('Delete command executed.');
        }
    });

    context.subscriptions.push(runCodeScaffold, pushSqlChanges, deleteCmd);
}

/**
 * Handles the Run Code Scaffold command for SQL files
 */
async function handleRunCodeScaffold(uri: vscode.Uri | undefined, selectedFiles: vscode.Uri[] | undefined): Promise<void> {
    // Collect all SQL files to process
    const filesToProcess: vscode.Uri[] = [];
    
    if (selectedFiles && selectedFiles.length > 0) {
        // Multiple files selected
        filesToProcess.push(...selectedFiles.filter(file => file.fsPath.toLowerCase().endsWith('.sql')));
    } else if (uri && uri.fsPath) {
        // Single file selected
        if (uri.fsPath.toLowerCase().endsWith('.sql')) {
            filesToProcess.push(uri);
        }
    } else {
        // Try to get the current active file
        const activeEditor = vscode.window.activeTextEditor;
        if (activeEditor && activeEditor.document.fileName.toLowerCase().endsWith('.sql')) {
            filesToProcess.push(vscode.Uri.file(activeEditor.document.fileName));
        }
    }
    
    if (filesToProcess.length === 0) {
        vscode.window.showErrorMessage('Please select one or more SQL files.');
        return;
    }
    
    // Extract schema and entity names from file paths
    const entities: string[] = [];
    for (const file of filesToProcess) {
        const entity = extractSchemaAndEntity(file.fsPath);
        if (entity) {
            entities.push(entity);
        } else {
            vscode.window.showWarningMessage(`Could not determine schema and entity name for: ${path.basename(file.fsPath)}`);
        }
    }
    
    if (entities.length === 0) {
        vscode.window.showErrorMessage('Could not extract entity names from selected SQL files.');
        return;
    }
    
    // Build the regen parameter (semicolon-separated list)
    const regenParam = entities.join(';');
    
    // Execute command in terminal
    const command = `Apstory.Scaffold.App -regen ${regenParam}`;
    const terminal = vscode.window.createTerminal('SQL Code Scaffold');
    terminal.show();
    terminal.sendText(command);
    
    vscode.window.showInformationMessage(`Running code scaffold for ${entities.length} file(s)...`);
}

/**
 * Extracts schema and entity name from SQL file path
 * Expected path structure: .../schema/Tables/EntityName.sql or .../schema/Stored Procedures/ProcName.sql
 */
export function extractSchemaAndEntity(filePath: string): string | null {
    try {
        // Normalize path separators to forward slashes
        const normalizedPath = filePath.replace(/\\/g, '/');
        
        // Get just the filename without extension
        const parts = normalizedPath.split('/');
        const fileNameWithExt = parts[parts.length - 1];
        const fileName = fileNameWithExt.replace(/\.sql$/i, '');
        
        // Find the index of "Tables" or "Stored Procedures"
        const tablesIndex = parts.findIndex(p => p.toLowerCase() === 'tables');
        const storedProcsIndex = parts.findIndex(p => p.toLowerCase() === 'stored procedures');
        
        let schema = 'dbo'; // default schema
        
        if (tablesIndex >= 1) {
            // Schema is the folder before "Tables"
            schema = parts[tablesIndex - 1];
        } else if (storedProcsIndex >= 1) {
            // Schema is the folder before "Stored Procedures"
            schema = parts[storedProcsIndex - 1];
        }
        
        return `${schema}.${fileName}`;
    } catch (error) {
        console.error('Error extracting schema and entity:', error);
        return null;
    }
}

/**
 * Helper function to validate SQL files
 */
export function validateSqlFile(uri: vscode.Uri | undefined): boolean {
    // If no URI is provided, try to get the current active file
    if (!uri || !uri.fsPath) {
        const activeEditor = vscode.window.activeTextEditor;
        if (activeEditor) {
            uri = vscode.Uri.file(activeEditor.document.fileName);
        } else {
            vscode.window.showErrorMessage('Please right-click a valid SQL file.');
            return false;
        }
    }
    
    // Check if the file is a SQL file
    if (!uri.fsPath.toLowerCase().endsWith('.sql')) {
        vscode.window.showErrorMessage('This command can only be used with SQL files.');
        return false;
    }
    
    return true;
}
