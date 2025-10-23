package com.apstory.scaffold.rider.actions

import com.apstory.scaffold.rider.utils.ScaffoldUtils
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CommonDataKeys
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.Messages
import org.jetbrains.plugins.terminal.ShellStartupOptions
import org.jetbrains.plugins.terminal.TerminalToolWindowManager

class GenerateSQLiteRepoAction : AnAction() {

    override fun update(e: AnActionEvent) {
        val file = e.getData(CommonDataKeys.VIRTUAL_FILE)
        e.presentation.isEnabledAndVisible = ScaffoldUtils.isTypeScriptFile(file)
    }

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val file = e.getData(CommonDataKeys.VIRTUAL_FILE) ?: return

        if (!ScaffoldUtils.isTypeScriptFile(file)) {
            Messages.showErrorDialog(
                project,
                "This command can only be used with TypeScript files.",
                "Generate SQLite Repository"
            )
            return
        }

        val solutionFolder = ScaffoldUtils.findSolutionFolder(file)
        if (solutionFolder == null) {
            Messages.showErrorDialog(
                project,
                "Could not find solution folder.",
                "Generate SQLite Repository"
            )
            return
        }

        val command = "Apstory.Scaffold.App -tsdalfolder -tsmodel \"${file.path}\""
        executeInTerminal(project, command, solutionFolder.path)

        Messages.showInfoMessage(
            project,
            "Generating SQLite repository for ${file.name}...",
            "Generate SQLite Repository"
        )
    }

    private fun executeInTerminal(project: Project, command: String, workingDirectory: String) {
        val terminalManager = TerminalToolWindowManager.getInstance(project)
        val shellStartupOptions = ShellStartupOptions.Builder()
            .workingDirectory(workingDirectory)
            .build()
        val shellWidget = terminalManager.createShellWidget(workingDirectory, "SQLite Repo", true, true)
        shellWidget.sendCommandToExecute(command)
    }
}
