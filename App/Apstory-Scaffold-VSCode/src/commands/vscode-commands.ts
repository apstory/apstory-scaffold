// Import necessary modules
import * as vscode from 'vscode';
import { ConfigUtil } from '../utils/config-util';

/**
 * Handles general VS Code related commands
 */
export function registerVSCodeCommands(context: vscode.ExtensionContext): void {
    // Register Configure command
    const configure = vscode.commands.registerCommand('apstoryScaffold.configure', async () => {
        await ConfigUtil.openConfigFile();
    });

    // Register Configure Help command
    const configureHelp = vscode.commands.registerCommand('apstoryScaffold.configureHelp', async () => {
        await ConfigUtil.openConfigReadme();
    });

    // Create a status bar item
    const statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
    statusBarItem.text = "$(gear) Scaffold";
    statusBarItem.tooltip = "Scaffold Tools";
    statusBarItem.command = "apstoryScaffold.showCommands";
    statusBarItem.show();
    
    // Register a command to show available commands
    const showCommands = vscode.commands.registerCommand('apstoryScaffold.showCommands', () => {
        showCommandsMenu();
    });

    context.subscriptions.push(configure, configureHelp, statusBarItem, showCommands);
}

/**
 * Shows a quick pick menu with commands based on the active file type
 */
export function showCommandsMenu(): void {
    const activeEditor = vscode.window.activeTextEditor;
    const fileExtension = activeEditor ? activeEditor.document.fileName.split('.').pop()?.toLowerCase() : '';
    
    let commands = [];
    
    // Always show Configure command
    commands.push({ label: "Configure", command: "apstoryScaffold.configure" });
    commands.push({ label: "Configuration Help", command: "apstoryScaffold.configureHelp" });
    
    // TypeScript file commands
    if (fileExtension === 'ts' || fileExtension === 'tsx') {
        commands.push({ label: "Generate SQLite Repository", command: "extension.generateSQLiteRepo" });
    }
    
    // SQL file commands
    if (fileExtension === 'sql') {
        commands.push({ label: "Run Code Scaffold", command: "apstoryScaffold.runCodeScaffold" });
        commands.push({ label: "Push SQL Changes", command: "apstoryScaffold.pushSqlChanges" });
        commands.push({ label: "Delete", command: "apstoryScaffold.delete" });
    }
    
    vscode.window.showQuickPick(commands).then(selection => {
        if (selection) {
            vscode.commands.executeCommand(selection.command);
        }
    });
}
