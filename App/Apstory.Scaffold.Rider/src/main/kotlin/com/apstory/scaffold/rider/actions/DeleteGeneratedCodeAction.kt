package com.apstory.scaffold.rider.actions

import com.apstory.scaffold.rider.utils.ScaffoldUtils
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CommonDataKeys
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.Messages
import org.jetbrains.plugins.terminal.ShellStartupOptions
import org.jetbrains.plugins.terminal.TerminalToolWindowManager

class DeleteGeneratedCodeAction : AnAction() {

    override fun update(e: AnActionEvent) {
        val file = e.getData(CommonDataKeys.VIRTUAL_FILE)
        e.presentation.isEnabledAndVisible = ScaffoldUtils.isSqlFile(file)
    }

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val files = e.getData(CommonDataKeys.VIRTUAL_FILE_ARRAY) ?: return

        val sqlFiles = files.filter { ScaffoldUtils.isSqlFile(it) }
        if (sqlFiles.isEmpty()) {
            Messages.showErrorDialog(project, "Please select one or more SQL files.", "Delete Generated Code")
            return
        }

        val entities = sqlFiles.mapNotNull { ScaffoldUtils.extractSchemaAndEntity(it.path) }
        if (entities.isEmpty()) {
            Messages.showErrorDialog(
                project,
                "Could not extract entity names from selected SQL files.",
                "Delete Generated Code"
            )
            return
        }

        val result = Messages.showYesNoDialog(
            project,
            "Are you sure you want to delete generated code for ${entities.size} file(s)?",
            "Delete Generated Code",
            Messages.getWarningIcon()
        )

        if (result != Messages.YES) {
            return
        }

        val deleteParam = entities.joinToString(";")
        val solutionFolder = ScaffoldUtils.findSolutionFolder(sqlFiles.first())

        if (solutionFolder == null) {
            Messages.showErrorDialog(
                project,
                "Could not find solution folder.",
                "Delete Generated Code"
            )
            return
        }

        val command = "Apstory.Scaffold.App -delete $deleteParam"
        executeInTerminal(project, command, solutionFolder.path)

        Messages.showInfoMessage(
            project,
            "Deleting generated code for ${entities.size} file(s)...",
            "Delete Generated Code"
        )
    }

    private fun executeInTerminal(project: Project, command: String, workingDirectory: String) {
        val terminalManager = TerminalToolWindowManager.getInstance(project)
        val shellStartupOptions = ShellStartupOptions.Builder()
            .workingDirectory(workingDirectory)
            .build()
        val shellWidget = terminalManager.createShellWidget(workingDirectory, "Delete Code", true, true)
        shellWidget.sendCommandToExecute(command)
    }
}
