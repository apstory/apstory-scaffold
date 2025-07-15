// Import necessary modules
import * as vscode from 'vscode';
import * as fs from 'fs';
import * as path from 'path';

/**
 * Configuration utility class
 */
export class ConfigUtil {
    // Store the extension path when initialized
    private static _extensionPath: string | undefined;

    /**
     * Initialize the config utility with the extension context
     * Call this during extension activation
     */
    public static initialize(context: vscode.ExtensionContext): void {
        this._extensionPath = context.extensionPath;
    }

    /**
     * Gets the extension path
     */
    public static getExtensionPath(): string {
        if (this._extensionPath) {
            return this._extensionPath;
        }
        
        // Try to get extension path from extension API
        const extension = vscode.extensions.getExtension('apstory.scaffold');
        if (extension) {
            return extension.extensionPath;
        }
        
        // Fallback: Use current working directory as a last resort during development
        return process.cwd();
    }

    /**
     * Gets the path to the config file
     */
    public static getConfigFilePath(): string {
        // Use the first workspace folder as the project directory
        const workspaceFolders = vscode.workspace.workspaceFolders;
        
        if (!workspaceFolders || workspaceFolders.length === 0) {
            throw new Error('No workspace folder found. Please open a folder or workspace.');
        }
        
        // Use the first workspace folder as the project root
        const projectRoot = workspaceFolders[0].uri.fsPath;
        
        // Create a .apstory folder in the project root for the config
        return path.join(projectRoot, '.apstory', 'config.json');
    }

    /**
     * Creates the config file if it doesn't exist
     */
    public static async ensureConfigFile(): Promise<boolean> {
        // Check if a workspace is open
        if (!vscode.workspace.workspaceFolders || vscode.workspace.workspaceFolders.length === 0) {
            throw new Error('No workspace folder found. Please open a folder or workspace.');
        }
        
        const configPath = this.getConfigFilePath();
        const configDir = path.dirname(configPath);

        try {
            // Create directory if it doesn't exist
            if (!fs.existsSync(configDir)) {
                fs.mkdirSync(configDir, { recursive: true });
            }

            // Create config file if it doesn't exist
            if (!fs.existsSync(configPath)) {
                // Get the template path
                const extensionPath = this.getExtensionPath();
                const templatePath = path.join(extensionPath, 'templates', 'config.template.json');
                const readmePath = path.join(extensionPath, 'templates', 'CONFIG_README.md');
                
                // Create default config with project-specific paths
                const workspaceFolder = vscode.workspace.workspaceFolders[0].uri.fsPath;
                const defaultConfig = {
                    SqlLiteRepoFolder: path.join(workspaceFolder, 'SqliteRepositories'),
                    Namespace: null,
                    SqlProject: null,
                    SqlDestination: null,
                    Variant: null
                };
                
                // Write the config file
                if (fs.existsSync(templatePath)) {
                    // Read template content
                    let templateContent = fs.readFileSync(templatePath, 'utf8');
                    
                    // Replace placeholder with project-specific path
                    templateContent = templateContent.replace(
                        "C:\\YourCustomPath\\SqlLiteRepositories", 
                        defaultConfig.SqlLiteRepoFolder.replace(/\\/g, '\\\\')
                    );
                    
                    fs.writeFileSync(configPath, templateContent);
                } else {
                    // Create default config
                    fs.writeFileSync(configPath, JSON.stringify(defaultConfig, null, 4));
                }
                
                // Copy README file if it exists
                if (fs.existsSync(readmePath)) {
                    fs.copyFileSync(readmePath, path.join(configDir, 'README.md'));
                }
                
                // Show a notification with a link to the README
                vscode.window.showInformationMessage(
                    'A new configuration file has been created. Would you like to view the configuration help?',
                    'View Help'
                ).then(selection => {
                    if (selection === 'View Help') {
                        this.openConfigReadme();
                    }
                });
                
                return true; // Config was created
            }
            
            return false; // Config already existed
        } catch (error) {
            console.error('Error ensuring config file:', error);
            throw error;
        }
    }

    /**
     * Opens the config file
     */
    public static async openConfigFile(): Promise<void> {
        try {
            // Check if a workspace is open
            if (!vscode.workspace.workspaceFolders || vscode.workspace.workspaceFolders.length === 0) {
                vscode.window.showErrorMessage('No workspace folder found. Please open a folder or workspace to configure the extension.');
                return;
            }
            
            await this.ensureConfigFile();
            const configPath = this.getConfigFilePath();
            
            // Open the config file
            const document = await vscode.workspace.openTextDocument(configPath);
            await vscode.window.showTextDocument(document);
            
            // If this is a first-time setup, offer to show the help
            const configCreated = await this.ensureConfigFile();
            if (!configCreated) {
                // Just show a subtle status bar notification for returning users
                vscode.window.setStatusBarMessage('Tip: Use "Configuration Help" to learn about available options', 5000);
            }
        } catch (error) {
            console.error('Failed to open config file:', error);
            
            // Try to create the default config directly
            try {
                const configPath = this.getConfigFilePath();
                const configDir = path.dirname(configPath);
                
                if (!fs.existsSync(configDir)) {
                    fs.mkdirSync(configDir, { recursive: true });
                }
                
                const defaultConfig = {
                    SqlLiteRepoFolder: "C:\\YourCustomPath\\SqlLiteRepositories",
                    Namespace: null,
                    SqlProject: null,
                    SqlDestination: null,
                    Variant: null
                };
                
                fs.writeFileSync(configPath, JSON.stringify(defaultConfig, null, 4));
                
                // Copy README file if it exists
                const extensionPath = this.getExtensionPath();
                const readmePath = path.join(extensionPath, 'templates', 'CONFIG_README.md');
                if (fs.existsSync(readmePath)) {
                    fs.copyFileSync(readmePath, path.join(configDir, 'README.md'));
                }
                
                // Try to open it again
                const document = await vscode.workspace.openTextDocument(configPath);
                await vscode.window.showTextDocument(document);
                
                vscode.window.showInformationMessage('Created a new project-specific configuration file.');
            } catch (fallbackError) {
                vscode.window.showErrorMessage(`Failed to open project config file: ${error}. Fallback also failed: ${fallbackError}`);
            }
        }
    }

    /**
     * Opens the config README file
     */
    public static async openConfigReadme(): Promise<void> {
        try {
            // Check if a workspace is open
            if (!vscode.workspace.workspaceFolders || vscode.workspace.workspaceFolders.length === 0) {
                vscode.window.showErrorMessage('No workspace folder found. Please open a folder or workspace.');
                return;
            }
            
            // Ensure the config directory exists
            await this.ensureConfigFile();
            
            const configPath = this.getConfigFilePath();
            const configDir = path.dirname(configPath);
            const readmePath = path.join(configDir, 'README.md');
            
            // Check if README exists in the config directory
            if (fs.existsSync(readmePath)) {
                // Open the README file
                const document = await vscode.workspace.openTextDocument(readmePath);
                await vscode.window.showTextDocument(document);
            } else {
                // Try to get the template README and copy it
                const extensionPath = this.getExtensionPath();
                const templateReadmePath = path.join(extensionPath, 'templates', 'CONFIG_README.md');
                
                if (fs.existsSync(templateReadmePath)) {
                    // Copy the README file
                    fs.copyFileSync(templateReadmePath, readmePath);
                    
                    // Open the README file
                    const document = await vscode.workspace.openTextDocument(readmePath);
                    await vscode.window.showTextDocument(document);
                } else {
                    vscode.window.showErrorMessage('Configuration help file not found.');
                }
            }
        } catch (error) {
            console.error('Failed to open config README:', error);
            vscode.window.showErrorMessage(`Failed to open configuration help: ${error}`);
        }
    }

    /**
     * Gets a configuration value
     */
    public static getConfigValue<T>(key: string, defaultValue: T): T {
        try {
            // Check if a workspace is open
            if (!vscode.workspace.workspaceFolders || vscode.workspace.workspaceFolders.length === 0) {
                console.error('No workspace folder found. Cannot read configuration.');
                return defaultValue;
            }
            
            const configPath = this.getConfigFilePath();
            
            if (!fs.existsSync(configPath)) {
                return defaultValue;
            }
            
            const configContent = fs.readFileSync(configPath, 'utf8');
            const config = JSON.parse(configContent);
            
            return config[key] !== undefined ? config[key] : defaultValue;
        } catch (error) {
            console.error(`Error getting config value for ${key}:`, error);
            return defaultValue;
        }
    }
}
