using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Sql;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Apstory.Scaffold.Domain.Service
{
    public class SqlTableUpdateService
    {
        public class TableUpdateResult
        {
            public bool Success { get; set; }
            public bool HasDataLoss { get; set; }
            public List<string> DataLossReasons { get; set; } = new();
            public List<string> SqlStatements { get; set; } = new();
            public string ErrorMessage { get; set; } = string.Empty;
        }

        public async Task<TableUpdateResult> GenerateTableUpdateScript(string connectionString, SqlTable sourceTable, bool blockOnDataLoss)
        {
            var result = new TableUpdateResult();

            try
            {
                // Get existing table schema from database
                var existingColumns = await GetExistingTableColumns(connectionString, sourceTable.Schema, sourceTable.TableName);
                
                if (existingColumns.Count == 0)
                {
                    // Table doesn't exist, no update needed (creation is handled elsewhere)
                    result.Success = false;
                    result.ErrorMessage = "Table does not exist in database";
                    return result;
                }

                // Compare and generate ALTER statements
                var compareResult = CompareAndGenerateAlterStatements(existingColumns, sourceTable, blockOnDataLoss);
                result.Success = compareResult.Success;
                result.HasDataLoss = compareResult.HasDataLoss;
                result.DataLossReasons = compareResult.DataLossReasons;
                result.SqlStatements = compareResult.SqlStatements;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private async Task<List<DatabaseColumn>> GetExistingTableColumns(string connectionString, string schemaName, string tableName)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT 
                        c.COLUMN_NAME as ColumnName,
                        c.DATA_TYPE as DataType,
                        CASE 
                            WHEN c.CHARACTER_MAXIMUM_LENGTH IS NOT NULL THEN CAST(c.CHARACTER_MAXIMUM_LENGTH AS VARCHAR)
                            WHEN c.NUMERIC_PRECISION IS NOT NULL AND c.NUMERIC_SCALE IS NOT NULL THEN 
                                CAST(c.NUMERIC_PRECISION AS VARCHAR) + ',' + CAST(c.NUMERIC_SCALE AS VARCHAR)
                            ELSE ''
                        END as DataTypeLength,
                        c.COLUMN_DEFAULT as DefaultValue,
                        CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END as IsNullable,
                        c.ORDINAL_POSITION as OrdinalPosition
                    FROM INFORMATION_SCHEMA.COLUMNS c
                    WHERE c.TABLE_SCHEMA = @SchemaName 
                    AND c.TABLE_NAME = @TableName
                    ORDER BY c.ORDINAL_POSITION";

                var columns = await connection.QueryAsync<DatabaseColumn>(query, new { SchemaName = schemaName, TableName = tableName });
                return columns.ToList();
            }
        }

        private TableUpdateResult CompareAndGenerateAlterStatements(List<DatabaseColumn> existingColumns, SqlTable sourceTable, bool blockOnDataLoss)
        {
            var result = new TableUpdateResult { Success = true };
            var alterStatements = new List<string>();

            // Create dictionaries for easier lookup
            var existingColumnsDict = existingColumns.ToDictionary(c => c.ColumnName, StringComparer.OrdinalIgnoreCase);
            var sourceColumnsDict = sourceTable.Columns.ToDictionary(c => c.ColumnName, StringComparer.OrdinalIgnoreCase);

            // Check for columns to add (in source but not in existing)
            foreach (var sourceColumn in sourceTable.Columns)
            {
                if (!existingColumnsDict.ContainsKey(sourceColumn.ColumnName))
                {
                    // New column - add it
                    var addColumnSql = GenerateAddColumnStatement(sourceTable.Schema, sourceTable.TableName, sourceColumn);
                    alterStatements.Add(addColumnSql);
                    Logger.LogInfo($"  + Adding column: {sourceColumn.ColumnName}");
                }
            }

            // Check for columns to drop (in existing but not in source)
            foreach (var existingColumn in existingColumns)
            {
                if (!sourceColumnsDict.ContainsKey(existingColumn.ColumnName))
                {
                    // Column removed - potential data loss
                    result.HasDataLoss = true;
                    result.DataLossReasons.Add($"Column [{existingColumn.ColumnName}] will be dropped");
                    
                    if (!blockOnDataLoss)
                    {
                        var dropColumnSql = GenerateDropColumnStatement(sourceTable.Schema, sourceTable.TableName, existingColumn.ColumnName);
                        alterStatements.Add(dropColumnSql);
                        Logger.LogWarn($"  - Dropping column: {existingColumn.ColumnName} (DATA LOSS)");
                    }
                }
            }

            // Check for columns to modify (exist in both but with different definitions)
            foreach (var sourceColumn in sourceTable.Columns)
            {
                if (existingColumnsDict.TryGetValue(sourceColumn.ColumnName, out var existingColumn))
                {
                    if (IsColumnModified(existingColumn, sourceColumn))
                    {
                        // Check if modification could cause data loss
                        bool modificationCausesDataLoss = CouldModificationCauseDataLoss(existingColumn, sourceColumn);
                        
                        if (modificationCausesDataLoss)
                        {
                            result.HasDataLoss = true;
                            result.DataLossReasons.Add($"Column [{sourceColumn.ColumnName}] modification may cause data loss (type: {existingColumn.DataType} -> {sourceColumn.DataType})");
                        }

                        if (!modificationCausesDataLoss || !blockOnDataLoss)
                        {
                            var alterColumnSql = GenerateAlterColumnStatement(sourceTable.Schema, sourceTable.TableName, sourceColumn);
                            alterStatements.Add(alterColumnSql);
                            Logger.LogInfo($"  * Modifying column: {sourceColumn.ColumnName}");
                        }
                    }
                }
            }

            // If blocking on data loss and data loss detected, clear statements
            if (blockOnDataLoss && result.HasDataLoss)
            {
                result.Success = false;
                result.SqlStatements.Clear();
                Logger.LogError($"Blocked update due to potential data loss. Use -blockondataloss false to proceed anyway.");
            }
            else
            {
                result.SqlStatements = alterStatements;
            }

            return result;
        }

        private bool IsColumnModified(DatabaseColumn existingColumn, SqlColumn sourceColumn)
        {
            // Normalize data types for comparison
            string existingDataType = NormalizeDataType(existingColumn.DataType, existingColumn.DataTypeLength);
            string sourceDataType = NormalizeDataType(sourceColumn.DataType, sourceColumn.DataTypeLength);

            if (!existingDataType.Equals(sourceDataType, StringComparison.OrdinalIgnoreCase))
                return true;

            if (existingColumn.IsNullable != sourceColumn.IsNullable)
                return true;

            // Note: We're not comparing default values as they can be represented differently
            // and may not be critical for updates

            return false;
        }

        private string NormalizeDataType(string dataType, string dataTypeLength)
        {
            dataType = dataType.ToUpperInvariant();

            // Handle types that need length
            if (!string.IsNullOrEmpty(dataTypeLength) && dataTypeLength != "-1")
            {
                if (dataType == "VARCHAR" || dataType == "NVARCHAR" || dataType == "CHAR" || dataType == "NCHAR")
                {
                    return $"{dataType}({dataTypeLength})";
                }
                if (dataType == "DECIMAL" || dataType == "NUMERIC")
                {
                    return $"{dataType}({dataTypeLength})";
                }
            }
            else if (dataTypeLength == "-1")
            {
                // MAX length
                if (dataType == "VARCHAR" || dataType == "NVARCHAR")
                {
                    return $"{dataType}(MAX)";
                }
            }

            return dataType;
        }

        private bool CouldModificationCauseDataLoss(DatabaseColumn existingColumn, SqlColumn sourceColumn)
        {
            string existingType = existingColumn.DataType.ToUpperInvariant();
            string sourceType = sourceColumn.DataType.ToUpperInvariant();

            // Changing from nullable to not nullable could cause data loss if NULL values exist
            if (existingColumn.IsNullable && !sourceColumn.IsNullable)
                return true;

            // Type conversions that could lose data
            if (existingType != sourceType)
            {
                // Changing to a potentially smaller type
                var potentialDataLossConversions = new[]
                {
                    (From: "BIGINT", To: "INT"),
                    (From: "INT", To: "SMALLINT"),
                    (From: "SMALLINT", To: "TINYINT"),
                    (From: "DECIMAL", To: "INT"),
                    (From: "FLOAT", To: "DECIMAL"),
                    (From: "NVARCHAR", To: "VARCHAR"), // Unicode to ANSI
                    (From: "DATETIME2", To: "DATETIME"), // Precision loss
                };

                if (potentialDataLossConversions.Any(c => c.From == existingType && c.To == sourceType))
                    return true;
            }

            // String length reduction
            if ((existingType == "VARCHAR" || existingType == "NVARCHAR" || existingType == "CHAR" || existingType == "NCHAR") &&
                (sourceType == "VARCHAR" || sourceType == "NVARCHAR" || sourceType == "CHAR" || sourceType == "NCHAR"))
            {
                if (!string.IsNullOrEmpty(existingColumn.DataTypeLength) && 
                    !string.IsNullOrEmpty(sourceColumn.DataTypeLength) &&
                    existingColumn.DataTypeLength != "-1" && 
                    sourceColumn.DataTypeLength != "-1")
                {
                    if (int.TryParse(existingColumn.DataTypeLength, out int existingLength) &&
                        int.TryParse(sourceColumn.DataTypeLength, out int sourceLength))
                    {
                        if (sourceLength < existingLength)
                            return true;
                    }
                }
            }

            // Decimal precision/scale reduction
            if ((existingType == "DECIMAL" || existingType == "NUMERIC") &&
                (sourceType == "DECIMAL" || sourceType == "NUMERIC"))
            {
                var existingParts = existingColumn.DataTypeLength?.Split(',');
                var sourceParts = sourceColumn.DataTypeLength?.Split(',');

                if (existingParts?.Length == 2 && sourceParts?.Length == 2)
                {
                    if (int.TryParse(existingParts[0], out int existingPrecision) &&
                        int.TryParse(sourceParts[0], out int sourcePrecision) &&
                        int.TryParse(existingParts[1], out int existingScale) &&
                        int.TryParse(sourceParts[1], out int sourceScale))
                    {
                        if (sourcePrecision < existingPrecision || sourceScale < existingScale)
                            return true;
                    }
                }
            }

            return false;
        }

        private string GenerateAddColumnStatement(string schema, string tableName, SqlColumn column)
        {
            var dataTypeStr = FormatDataType(column.DataType, column.DataTypeLength);
            var nullableStr = column.IsNullable ? "NULL" : "NOT NULL";
            // Note: DefaultValue comes from the parsed SQL file and is trusted source code, not user input
            var defaultStr = string.IsNullOrEmpty(column.DefaultValue) ? "" : $" {column.DefaultValue}";

            return $"ALTER TABLE [{schema}].[{tableName}] ADD [{column.ColumnName}] {dataTypeStr} {nullableStr}{defaultStr};";
        }

        private string GenerateDropColumnStatement(string schema, string tableName, string columnName)
        {
            return $"ALTER TABLE [{schema}].[{tableName}] DROP COLUMN [{columnName}];";
        }

        private string GenerateAlterColumnStatement(string schema, string tableName, SqlColumn column)
        {
            var dataTypeStr = FormatDataType(column.DataType, column.DataTypeLength);
            var nullableStr = column.IsNullable ? "NULL" : "NOT NULL";

            return $"ALTER TABLE [{schema}].[{tableName}] ALTER COLUMN [{column.ColumnName}] {dataTypeStr} {nullableStr};";
        }

        private string FormatDataType(string dataType, string dataTypeLength)
        {
            dataType = dataType.ToUpperInvariant();

            if (string.IsNullOrEmpty(dataTypeLength))
                return dataType;

            if (dataType == "VARCHAR" || dataType == "NVARCHAR" || dataType == "CHAR" || dataType == "NCHAR" ||
                dataType == "BINARY" || dataType == "VARBINARY")
            {
                return $"{dataType}({dataTypeLength})";
            }

            if (dataType == "DECIMAL" || dataType == "NUMERIC")
            {
                return $"{dataType}({dataTypeLength})";
            }

            return dataType;
        }

        public class DatabaseColumn
        {
            public string ColumnName { get; set; } = string.Empty;
            public string DataType { get; set; } = string.Empty;
            public string DataTypeLength { get; set; } = string.Empty;
            public string? DefaultValue { get; set; }
            public bool IsNullable { get; set; }
            public int OrdinalPosition { get; set; }
        }
    }
}
