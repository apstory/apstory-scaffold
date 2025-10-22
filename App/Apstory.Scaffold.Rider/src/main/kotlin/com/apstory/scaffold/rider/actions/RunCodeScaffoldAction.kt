package com.apstory.scaffold.rider.actions

import com.apstory.scaffold.rider.utils.ScaffoldUtils
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CommonDataKeys
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.Messages
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.terminal.TerminalExecutionConsole
import org.jetbrains.plugins.terminal.ShellTerminalWidget
import org.jetbrains.plugins.terminal.TerminalView

class RunCodeScaffoldAction : AnAction() {

    override fun update(e: AnActionEvent) {
        val file = e.getData(CommonDataKeys.VIRTUAL_FILE)
        e.presentation.isEnabledAndVisible = ScaffoldUtils.isSqlFile(file)
    }

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val files = e.getData(CommonDataKeys.VIRTUAL_FILE_ARRAY) ?: return

        val sqlFiles = files.filter { ScaffoldUtils.isSqlFile(it) }
        if (sqlFiles.isEmpty()) {
            Messages.showErrorDialog(project, "Please select one or more SQL files.", "Code Scaffold")
            return
        }

        val entities = sqlFiles.mapNotNull { ScaffoldUtils.extractSchemaAndEntity(it.path) }
        if (entities.isEmpty()) {
            Messages.showErrorDialog(
                project,
                "Could not extract entity names from selected SQL files.",
                "Code Scaffold"
            )
            return
        }

        val regenParam = entities.joinToString(";")
        val solutionFolder = ScaffoldUtils.findSolutionFolder(sqlFiles.first())

        if (solutionFolder == null) {
            Messages.showErrorDialog(
                project,
                "Could not find solution folder. Please ensure you're working in a solution directory.",
                "Code Scaffold"
            )
            return
        }

        val command = "Apstory.Scaffold.App -regen $regenParam"
        executeInTerminal(project, command, solutionFolder.path)

        Messages.showInfoMessage(
            project,
            "Running code scaffold for ${entities.size} file(s)...",
            "Code Scaffold"
        )
    }

    private fun executeInTerminal(project: Project, command: String, workingDirectory: String) {
        val terminalView = TerminalView.getInstance(project)
        val widget = terminalView.createLocalShellWidget(workingDirectory, "Scaffold")
        widget.executeCommand(command)
    }
}
