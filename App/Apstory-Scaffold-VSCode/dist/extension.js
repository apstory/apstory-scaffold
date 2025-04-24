"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

// src/extension.ts
var extension_exports = {};
__export(extension_exports, {
  activate: () => activate
});
module.exports = __toCommonJS(extension_exports);
var vscode = __toESM(require("vscode"));
var { exec } = require("child_process");
var path = require("path");
function activate(context) {
  let disposable = vscode.commands.registerCommand("extension.generateSQLiteRepo", async (uri) => {
    if (!uri || !uri.fsPath) {
      vscode.window.showErrorMessage("Please right-click a valid file or folder.");
      return;
    }
    console.log(`Got ${uri.fsPath}`);
    const repoPath = await vscode.window.showSaveDialog({
      title: "Save Repository",
      saveLabel: "Save"
    });
    const selectedModelPath = uri.fsPath;
    const repositoryPath = repoPath?.fsPath;
    const command = `generate-sqlite-repo -model "${selectedModelPath}" -output ""${repositoryPath}`;
    const terminal = vscode.window.createTerminal("SQLite Repo Generator");
    terminal.show();
    terminal.sendText(command);
  });
  context.subscriptions.push(disposable);
}
function deactivate() {
}
module.exports = {
  activate,
  deactivate
};
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  activate
});
//# sourceMappingURL=extension.js.map
