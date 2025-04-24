## Debugging

Press F5 in VSCode to run the project

## Release

npm install -g @vscode/vsce
vsce pack
This generates a .vsix file, which you can install in VSCode by dragging it into the Extensions tab.


## Features

Generate full transaction search screens

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
