## Debugging

Press F5 in VSCode to run the project

## Release

npm install -g @vscode/vsce
vsce pack
This generates a .vsix file, which you can install in VSCode by dragging it into the Extensions tab.


## Features

### SQL Code Scaffolding
Right-click on one or more SQL files (tables or stored procedures) and select **Scaffold > Run Code Scaffold** to automatically generate:
- C# Models
- Repository layer code
- Service layer code
- Database stored procedures

The extension extracts the schema and entity name from the SQL file path and executes `Apstory.Scaffold.App -regen dbo.[NameOfTableOrProc]`.

**Supported file structures:**
- `<schema>/Tables/<TableName>.sql` - for table files
- `<schema>/Stored Procedures/<ProcName>.sql` - for stored procedure files

**Multi-file support:** Select multiple SQL files in the explorer to scaffold them all at once.

### SQLite Repository Generation
Generate SQLite repository TypeScript files from TypeScript model files.

### Other Features
- Push SQL changes to a database
- Delete generated code
- Configuration management

## Requirements

Dependent on the Apstory.Scaffold.App application to actually run the code scaffolding instance.

## Extension Settings

Include if your extension adds any VS Code settings through the `contributes.configuration` extension point.

For example:

This extension contributes the following settings:

* `myExtension.enable`: Enable/disable this extension.
* `myExtension.thing`: Set to `blah` to do something.

## Known Issues

No known issues

## Release Notes

### 1.0.0

Initial release

## Following extension guidelines

Ensure that you've read through the extensions guidelines and follow the best practices for creating your extension.

* [Extension Guidelines](https://code.visualstudio.com/api/references/extension-guidelines)
