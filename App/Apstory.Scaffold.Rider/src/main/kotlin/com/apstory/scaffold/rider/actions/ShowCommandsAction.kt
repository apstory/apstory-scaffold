package com.apstory.scaffold.rider.actions

import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.ui.Messages

class ShowCommandsAction : AnAction() {

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return

        val message = """
            <html>
            <h3>Apstory Scaffold Commands</h3>

            <h4>SQL File Commands (Right-click on .sql files):</h4>
            <ul>
              <li><b>Run Code Scaffold</b> - Generate C# code from SQL table definitions</li>
              <li><b>Push SQL Changes</b> - Push SQL changes to database (requires SQL Destination configuration)</li>
              <li><b>Delete Generated Code</b> - Remove generated code files</li>
            </ul>

            <h4>TypeScript File Commands (Right-click on .ts files):</h4>
            <ul>
              <li><b>Generate SQLite Repository</b> - Create TypeScript repositories for SQLite</li>
            </ul>

            <h4>Configuration:</h4>
            <ul>
              <li><b>Configure</b> - Set SQL Destination and PowerShell script path</li>
            </ul>

            <p>For more information, visit: <a href="https://github.com/Apstory/Apstory.Scaffold">GitHub Repository</a></p>
            </html>
        """.trimIndent()

        Messages.showMessageDialog(
            project,
            message,
            "Apstory Scaffold Commands",
            Messages.getInformationIcon()
        )
    }
}
