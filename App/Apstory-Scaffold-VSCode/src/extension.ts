// Import necessary modules
import * as vscode from 'vscode';
const { exec } = require('child_process');
const path = require('path');

/**
 * @param {vscode.ExtensionContext} context
 */
export function activate(context: vscode.ExtensionContext) {
    let disposable = vscode.commands.registerCommand('extension.generateSQLiteRepo', async (uri) => {
        if (!uri || !uri.fsPath) {
            vscode.window.showErrorMessage('Please right-click a valid file or folder.');
            return;
        }
        
		console.log(`Got ${uri.fsPath}`);

        // Step 1: Open a file picker restricted to app/model/**/*.ts
        const repoPath = await vscode.window.showSaveDialog({
            title: 'Save Repository',
			saveLabel: 'Save'
        });
		//defaultUri: vscode.Uri.file(path.join(vscode.workspace.asRelativePath || '', 'app', 'model')),

        // if (!files || files.length === 0) {
        //     vscode.window.showErrorMessage('No file selected.');
        //     return;
        // }


		// Step 1: Prompt user to select a model
        // const models = ['User', 'Product', 'Order']; // Replace with dynamic fetching if needed
        // const selectedModel = await vscode.window.showQuickPick(models, {
        //     placeHolder: 'Select a model for SQLite repository',
        // });
        
        // if (!selectedModel) {
        //     vscode.window.showInformationMessage('Operation cancelled.');
        //     return;
        // }

		const selectedModelPath = uri.fsPath;
        const repositoryPath = repoPath?.fsPath;

        // Step 3: Execute command interactively
        const command = `generate-sqlite-repo -model "${selectedModelPath}" -output ""${repositoryPath}`;
        const terminal = vscode.window.createTerminal('SQLite Repo Generator');
        terminal.show();
        terminal.sendText(command);
    });

    context.subscriptions.push(disposable);
}

function deactivate() {}

module.exports = {
    activate,
    deactivate
};