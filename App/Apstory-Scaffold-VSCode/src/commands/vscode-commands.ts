// Import necessary modules
import * as vscode from 'vscode';

/**
 * Handles general VS Code related commands
 */
export function registerVSCodeCommands(context: vscode.ExtensionContext): void {
    // Register Configure command
    const configure = vscode.commands.registerCommand('apstoryScaffold.configure', (uri) => {
        vscode.window.showInformationMessage('Configure command executed.');
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

    context.subscriptions.push(configure, statusBarItem, showCommands);
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
