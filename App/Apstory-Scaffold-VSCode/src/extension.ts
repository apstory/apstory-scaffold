// Import necessary modules
import * as vscode from 'vscode';
import { registerTypescriptCommands } from './commands/typescript-commands';
import { registerSqlCommands } from './commands/sql-commands';
import { registerVSCodeCommands } from './commands/vscode-commands';
import { ConfigUtil } from './utils/config-util';

/**
 * @param {vscode.ExtensionContext} context
 */
export function activate(context: vscode.ExtensionContext) {
    // Initialize the configuration utility
    ConfigUtil.initialize(context);
    
    // Show a notification that the extension has been activated
    vscode.window.showInformationMessage('Scaffold extension has been activated!');
    
    console.log('Scaffold extension is now active!');
    
    // Register all command groups
    registerVSCodeCommands(context);
    registerTypescriptCommands(context);
    registerSqlCommands(context);
}

function deactivate() {}

module.exports = {
    activate,
    deactivate
};