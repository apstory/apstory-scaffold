package com.apstory.scaffold.rider.actions

import com.apstory.scaffold.rider.services.ScaffoldConfigService
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.ui.DialogWrapper
import com.intellij.ui.components.JBLabel
import com.intellij.ui.components.JBTextField
import com.intellij.util.ui.FormBuilder
import javax.swing.JComponent
import javax.swing.JPanel

class ConfigureAction : AnAction() {

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val configService = ScaffoldConfigService.getInstance()

        val dialog = ConfigDialog(configService)
        if (dialog.showAndGet()) {
            configService.state.sqlDestination = dialog.sqlDestinationField.text
            configService.state.powershellScript = dialog.powershellScriptField.text
        }
    }

    private class ConfigDialog(private val configService: ScaffoldConfigService) : DialogWrapper(true) {
        val sqlDestinationField = JBTextField(configService.state.sqlDestination, 50)
        val powershellScriptField = JBTextField(configService.state.powershellScript, 50)

        init {
            title = "Apstory Scaffold Configuration"
            init()
        }

        override fun createCenterPanel(): JComponent {
            sqlDestinationField.toolTipText = "Database connection string for pushing SQL changes"
            powershellScriptField.toolTipText = "Path to TypeScript generation PowerShell script (relative or absolute)"

            return FormBuilder.createFormBuilder()
                .addLabeledComponent(JBLabel("SQL Destination (Connection String):"), sqlDestinationField, 1, false)
                .addLabeledComponent(JBLabel("PowerShell Script:"), powershellScriptField, 1, false)
                .addComponentFillVertically(JPanel(), 0)
                .panel
        }
    }
}
