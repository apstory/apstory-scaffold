// Import necessary modules
import * as vscode from 'vscode';
import * as path from 'path';
import { ConfigUtil } from '../utils/config-util';

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
        
        // Get the output folder from config
        let outputFolder = ConfigUtil.getConfigValue<string>('SqlLiteRepoFolder', '');
        
        // If output folder is not set, open the config file
        if (!outputFolder) {
            const configCreated = await ConfigUtil.ensureConfigFile();
            await ConfigUtil.openConfigFile();
            
            if (configCreated) {
                vscode.window.showInformationMessage('Please configure the SQLite repository output folder and try again.');
            } else {
                vscode.window.showErrorMessage('SqlLiteRepoFolder is not set in the config file.');
            }
            return;
        }
        
        const selectedModelPath = uri.fsPath;
        const fileName = path.basename(selectedModelPath, path.extname(selectedModelPath)) + '.Repository.cs';
        const repositoryPath = path.join(outputFolder, fileName);
        
        // Ensure the output directory exists
        const fs = require('fs');
        if (!fs.existsSync(outputFolder)) {
            fs.mkdirSync(outputFolder, { recursive: true });
        }

        // Execute command interactively
        const command = `Apstory.Scaffold.App -tsModel "${selectedModelPath}" -tsdalfolder "${repositoryPath}"`;
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
