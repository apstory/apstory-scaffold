package com.apstory.scaffold.rider.actions

import com.apstory.scaffold.rider.services.ScaffoldConfigService
import com.apstory.scaffold.rider.utils.ScaffoldUtils
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.CommonDataKeys
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.Messages
import org.jetbrains.plugins.terminal.TerminalView

class PushSqlChangesAction : AnAction() {

    override fun update(e: AnActionEvent) {
        val file = e.getData(CommonDataKeys.VIRTUAL_FILE)
        e.presentation.isEnabledAndVisible = ScaffoldUtils.isSqlFile(file)
    }

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val files = e.getData(CommonDataKeys.VIRTUAL_FILE_ARRAY) ?: return

        val config = ScaffoldConfigService.getInstance().state
        if (config.sqlDestination.isBlank()) {
            Messages.showErrorDialog(
                project,
                "SQL Destination is not configured. Please configure it first.",
                "Push SQL Changes"
            )
            return
        }

        val sqlFiles = files.filter { ScaffoldUtils.isSqlFile(it) }
        if (sqlFiles.isEmpty()) {
            Messages.showErrorDialog(project, "Please select one or more SQL files.", "Push SQL Changes")
            return
        }

        val entities = sqlFiles.mapNotNull { ScaffoldUtils.extractSchemaAndEntity(it.path) }
        if (entities.isEmpty()) {
            Messages.showErrorDialog(
                project,
                "Could not extract entity names from selected SQL files.",
                "Push SQL Changes"
            )
            return
        }

        val pushParam = entities.joinToString(";")
        val solutionFolder = ScaffoldUtils.findSolutionFolder(sqlFiles.first())

        if (solutionFolder == null) {
            Messages.showErrorDialog(
                project,
                "Could not find solution folder.",
                "Push SQL Changes"
            )
            return
        }

        val command = "Apstory.Scaffold.App -sqlpush $pushParam -sqldestination \"${config.sqlDestination}\""
        executeInTerminal(project, command, solutionFolder.path)

        Messages.showInfoMessage(
            project,
            "Pushing SQL changes for ${entities.size} file(s)...",
            "Push SQL Changes"
        )
    }

    private fun executeInTerminal(project: Project, command: String, workingDirectory: String) {
        val terminalView = TerminalView.getInstance(project)
        val widget = terminalView.createLocalShellWidget(workingDirectory, "SQL Push")
        widget.executeCommand(command)
    }
}
