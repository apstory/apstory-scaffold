package com.apstory.scaffold.rider.utils

import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import java.io.File

object ScaffoldUtils {

    /**
     * Extracts schema and entity name from SQL file path
     * Expected path structure: .../schema/Tables/EntityName.sql or .../schema/Stored Procedures/ProcName.sql
     */
    fun extractSchemaAndEntity(filePath: String): String? {
        try {
            val parts = filePath.replace("\\", "/").split("/")
            val fileName = parts.last().removeSuffix(".sql")

            val tablesIndex = parts.indexOfFirst { it.equals("Tables", ignoreCase = true) }
            val storedProcsIndex = parts.indexOfFirst { it.equals("Stored Procedures", ignoreCase = true) }

            val schema = when {
                tablesIndex >= 1 -> parts[tablesIndex - 1]
                storedProcsIndex >= 1 -> parts[storedProcsIndex - 1]
                else -> "dbo"
            }

            return "$schema.$fileName"
        } catch (e: Exception) {
            return null
        }
    }

    /**
     * Finds the closest solution folder containing a .sln file
     */
    fun findSolutionFolder(file: VirtualFile): VirtualFile? {
        var current: VirtualFile? = if (file.isDirectory) file else file.parent

        while (current != null) {
            val slnFiles = current.children.filter { it.extension == "sln" }
            if (slnFiles.isNotEmpty()) {
                val hasNonDbSln = slnFiles.any { !it.name.contains(".DB.") }
                if (hasNonDbSln) {
                    return current
                }
            }
            current = current.parent
        }

        return null
    }

    /**
     * Checks if a file is a SQL file
     */
    fun isSqlFile(file: VirtualFile?): Boolean {
        return file?.extension?.equals("sql", ignoreCase = true) == true
    }

    /**
     * Checks if a file is a TypeScript file
     */
    fun isTypeScriptFile(file: VirtualFile?): Boolean {
        return file?.extension?.equals("ts", ignoreCase = true) == true
    }
}
