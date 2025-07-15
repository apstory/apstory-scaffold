// Import necessary modules
import * as vscode from 'vscode';

/**
 * Handles TypeScript related commands
 */
export function registerTypescriptCommands(context: vscode.ExtensionContext): void {
    // Register Generate SQLite Repository command
    const generateSQLiteRepo = vscode.commands.registerCommand('extension.generateSQLiteRepo', async (uri) => {
        // If no URI is provided, try to get the current active file
        if (!uri || !uri.fsPath) {
            const activeEditor = vscode.window.activeTextEditor;
            if (activeEditor) {
                uri = vscode.Uri.file(activeEditor.document.fileName);
            } else {
                vscode.window.showErrorMessage('Please right-click a valid TypeScript file or folder.');
                return;
            }
        }
        
        // Check if the file is a TypeScript file
        if (!uri.fsPath.toLowerCase().endsWith('.ts') && !uri.fsPath.toLowerCase().endsWith('.tsx')) {
            vscode.window.showErrorMessage('This command can only be used with TypeScript files.');
            return;
        }
        
        const repoPath = await vscode.window.showSaveDialog({
            title: 'Save Repository',
            saveLabel: 'Save'
        });

        const selectedModelPath = uri.fsPath;
        const repositoryPath = repoPath?.fsPath;

        if (!repositoryPath) {
            vscode.window.showErrorMessage('Please select a valid path to save the repository.');
            return;
        }

        // Execute command interactively
        const command = `Apstory.Scaffold.App -model "${selectedModelPath}" -output "${repositoryPath}"`;
        const terminal = vscode.window.createTerminal('SQLite Repo Generator');
        terminal.show();
        terminal.sendText(command);
    });

    context.subscriptions.push(generateSQLiteRepo);
}

/**
 * Helper function to validate TypeScript files
 */
export function validateTypeScriptFile(uri: vscode.Uri | undefined): boolean {
    // If no URI is provided, try to get the current active file
    if (!uri || !uri.fsPath) {
        const activeEditor = vscode.window.activeTextEditor;
        if (activeEditor) {
            uri = vscode.Uri.file(activeEditor.document.fileName);
        } else {
            vscode.window.showErrorMessage('Please right-click a valid TypeScript file.');
            return false;
        }
    }
    
    // Check if the file is a TypeScript file
    if (!uri.fsPath.toLowerCase().endsWith('.ts') && !uri.fsPath.toLowerCase().endsWith('.tsx')) {
        vscode.window.showErrorMessage('This command can only be used with TypeScript files.');
        return false;
    }
    
    return true;
}
