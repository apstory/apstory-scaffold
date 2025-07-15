// Import necessary modules
import * as vscode from 'vscode';

/**
 * Handles SQL related commands
 */
export function registerSqlCommands(context: vscode.ExtensionContext): void {
    // Register Run Code Scaffold command
    const runCodeScaffold = vscode.commands.registerCommand('apstoryScaffold.runCodeScaffold', (uri) => {
        if (validateSqlFile(uri)) {
            vscode.window.showInformationMessage('Run Code Scaffold command executed.');
        }
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
