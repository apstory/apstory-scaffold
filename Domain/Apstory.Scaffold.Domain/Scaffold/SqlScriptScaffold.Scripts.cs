using Apstory.Scaffold.Domain.Service;
using Apstory.Scaffold.Model.Config;
using Apstory.Scaffold.Model.Sql;
using System.Text;

namespace Apstory.Scaffold.Domain.Scaffold
{
    public partial class SqlScriptFileScaffold
    {
        private string[] skipDTDefaults = new string[] { "CreateDT", "UpdateDT" };

        public string GenerateInsertUpdateProcedure(SqlTable table, bool generateMergeVariant)
        {
            var sb = new StringBuilder();

            var primaryColumn = GetPrimaryColumn(table);

            // Start the SQL procedure creation
            sb.AppendLine($"/****** Object:  StoredProcedure [{table.Schema}].[zgen_{table.TableName}_InsUpd] ******/");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine("-- Description    : Insert Update " + table.TableName);
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine();

            sb.AppendLine($"CREATE   PROCEDURE [{table.Schema}].[zgen_{table.TableName}_InsUpd]");

            // Add parameters for each column
            sb.Append("  (");

            var sortedColumns = GetSortedColumnsByNullableDefaultType(table);
            var sortedColumnsNoDt = sortedColumns.Where(s => !skipDTDefaults.Contains(s.ColumnName)).ToList();
            var sortedColumnsNoDtNoPK = sortedColumnsNoDt.Where(s => s.ColumnName != primaryColumn.ColumnName).ToList();

            foreach (var column in sortedColumnsNoDt)
                sb.Append($"@{column.ColumnName} {column.DataType}{(string.IsNullOrEmpty(column.DataTypeLength) ? "" : $"({column.DataTypeLength})")}{((column.IsNullable || column.ColumnName == primaryColumn.ColumnName) ? "=NULL" : "")},");

            sb.Append("@RetMsg NVARCHAR(MAX) OUTPUT");
            sb.AppendLine(")");

            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("  DECLARE @InitialTransCount INT = @@TRANCOUNT;");
            sb.AppendLine("  DECLARE @TranName varchar(32) = OBJECT_NAME(@@PROCID);");
            sb.AppendLine();

            sb.AppendLine("  BEGIN TRY");
            sb.AppendLine("    IF @InitialTransCount = 0 BEGIN TRANSACTION @TranName");
            sb.AppendLine();

            var primaryIsUniqueIdentifier = primaryColumn.DataType.Equals("UNIQUEIDENTIFIER", StringComparison.OrdinalIgnoreCase);
            if (generateMergeVariant && primaryIsUniqueIdentifier)
            {
                sb.AppendLine($"    IF (@{primaryColumn.ColumnName} IS NULL)");
                sb.AppendLine($"    BEGIN");
                sb.AppendLine($"        SET @{primaryColumn.ColumnName} = NEWID()");
                sb.AppendLine($"    END");
                sb.AppendLine();

                sb.AppendLine($"    MERGE INTO [{table.Schema}].[{table.TableName}] AS target");
                sb.AppendLine($"    USING (SELECT @{primaryColumn.ColumnName} AS {primaryColumn.ColumnName}) AS source");
                sb.AppendLine($"    ON target.{primaryColumn.ColumnName} = source.{primaryColumn.ColumnName}");
                sb.AppendLine($"    WHEN MATCHED THEN");
                sb.AppendLine($"        UPDATE SET ");

                foreach (var column in sortedColumnsNoDtNoPK)
                    sb.AppendLine($"            [{column.ColumnName}]=@{column.ColumnName},");

                sb.AppendLine($"            [UpdateDT]=GETDATE()");
                sb.AppendLine($"    WHEN NOT MATCHED THEN");
                sb.Append($"        INSERT (");

                foreach (var column in sortedColumnsNoDt)
                    sb.Append($"[{column.ColumnName}],");

                sb.Length--;
                sb.AppendLine(")");
                sb.Append("        VALUES (");

                foreach (var column in sortedColumnsNoDt)
                    sb.Append($"@{column.ColumnName},");

                sb.Length--;
                sb.AppendLine(");");
                sb.AppendLine();
                sb.AppendLine($"    SELECT * FROM [{table.Schema}].[{table.TableName}] WHERE {primaryColumn.ColumnName} = @{primaryColumn.ColumnName}");
            }
            else
            {
                sb.AppendLine($"    IF @{primaryColumn.ColumnName} IS NULL");
                sb.AppendLine("    BEGIN");
                if (!primaryIsUniqueIdentifier)
                {
                    sb.AppendLine($"      INSERT INTO [{table.Schema}].[{table.TableName}]");
                    sb.Append("        (");

                    foreach (var column in sortedColumnsNoDtNoPK)
                        sb.Append($"[{column.ColumnName}],");
                    sb.Length--;
                    sb.AppendLine(")");
                    sb.AppendLine("      VALUES");
                    sb.Append("        (");

                    foreach (var column in sortedColumnsNoDtNoPK)
                        sb.Append($"@{column.ColumnName},");
                    sb.Length--;
                    sb.AppendLine(");");

                    sb.AppendLine($"      SELECT * FROM [{table.Schema}].[{table.TableName}] WHERE [{primaryColumn.ColumnName}] = SCOPE_IDENTITY();");
                }
                else
                {
                    sb.AppendLine($"      SET @{primaryColumn.ColumnName} = NEWID();");
                    sb.AppendLine($"      INSERT INTO [{table.Schema}].[{table.TableName}]");
                    sb.Append("        (");

                    foreach (var column in sortedColumnsNoDt)
                        sb.Append($"[{column.ColumnName}],");
                    sb.Length--;
                    sb.AppendLine(")");
                    sb.AppendLine("      VALUES");
                    sb.Append("        (");

                    foreach (var column in sortedColumnsNoDt)
                        sb.Append($"@{column.ColumnName},");
                    sb.Length--;
                    sb.AppendLine(");");

                    sb.AppendLine($"      SELECT * FROM [{table.Schema}].[{table.TableName}] WHERE [{primaryColumn.ColumnName}] = @{primaryColumn.ColumnName};");
                }
                sb.AppendLine("    END");
                sb.AppendLine("  ELSE");
                sb.AppendLine("    BEGIN");
                sb.AppendLine($"      UPDATE [{table.Schema}].[{table.TableName}]");
                sb.Append("        SET ");

                foreach (var column in sortedColumnsNoDtNoPK)
                    sb.Append($"[{column.ColumnName}]=@{column.ColumnName},");

                sb.Append($"[UpdateDT]=GETDATE()");
                sb.AppendLine("");
                sb.AppendLine($"        WHERE ([{primaryColumn.ColumnName}] = @{primaryColumn.ColumnName});");

                sb.AppendLine($"      SELECT * FROM [{table.Schema}].[{table.TableName}] WHERE [{primaryColumn.ColumnName}] = @{primaryColumn.ColumnName};");
                sb.AppendLine("    END");
            }

            sb.AppendLine();
            sb.AppendLine("    IF @@ERROR <> 0 BEGIN GOTO errorMsg_section END");
            sb.AppendLine();

            sb.AppendLine("    IF @InitialTransCount = 0 COMMIT TRANSACTION @TranName");
            sb.AppendLine("    SET @RetMsg = LTRIM(ISNULL(@RetMsg, '') + 'Successful')");
            sb.AppendLine("    RETURN 0");
            sb.AppendLine();

            sb.AppendLine("  errorMsg_section:");
            sb.AppendLine("    SET @RetMsg = LTRIM(ISNULL(@RetMsg, '') + ' SQLErrMSG: ' + ISNULL(ERROR_MESSAGE(), ''))");
            sb.AppendLine("    GOTO error_section");
            sb.AppendLine();

            sb.AppendLine("  error_section:");
            sb.AppendLine("    SET @RetMsg = ISNULL(@RetMsg, '')");
            sb.AppendLine("    IF @InitialTransCount = 0 ROLLBACK TRANSACTION @TranName");
            sb.AppendLine("    RETURN 1");

            sb.AppendLine("  END TRY");
            sb.AppendLine("  BEGIN CATCH");
            sb.AppendLine("    IF @InitialTransCount = 0 ROLLBACK TRANSACTION @TranName");
            sb.AppendLine("    SET @RetMsg = LTRIM(ISNULL(@RetMsg, '') + ' SQLErrMSG: ' + ISNULL(ERROR_MESSAGE(), ''))");
            sb.AppendLine("    RETURN 1");
            sb.AppendLine("  END CATCH");
            sb.AppendLine("END");

            return sb.ToString();
        }

        public string GenerateGetByIdProcedure(SqlTable table)
        {
            var sb = new StringBuilder();

            // Start the SQL procedure creation
            sb.AppendLine($"/****** Object:  StoredProcedure [{table.Schema}].[zgen_{table.TableName}_GetById] ******/");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine("-- Description    : Select By Id " + table.TableName);
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine();

            sb.AppendLine($"CREATE   PROCEDURE [{table.Schema}].[zgen_{table.TableName}_GetById]");

            var primaryKeyColumn = GetPrimaryColumn(table);

            // Add parameters
            sb.AppendLine($"  (@{primaryKeyColumn.ColumnName} {primaryKeyColumn.DataType}{(string.IsNullOrEmpty(primaryKeyColumn.DataTypeLength) ? "" : $"({primaryKeyColumn.DataTypeLength})")}, @IsActive bit=NULL)");

            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("  SET NOCOUNT ON;");
            sb.AppendLine("  SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;");
            sb.AppendLine();
            sb.AppendLine($"  IF @{primaryKeyColumn.ColumnName} IS NULL");
            sb.AppendLine("  BEGIN");
            sb.AppendLine("    IF @IsActive IS NULL");
            sb.AppendLine($"      SELECT * FROM [{table.Schema}].[{table.TableName}] ORDER BY [{primaryKeyColumn.ColumnName}] ASC;");
            sb.AppendLine("    ELSE");
            sb.AppendLine($"      SELECT * FROM [{table.Schema}].[{table.TableName}] WHERE [IsActive] = @IsActive ORDER BY [{primaryKeyColumn.ColumnName}] ASC;");
            sb.AppendLine("  END");
            sb.AppendLine("  ELSE");
            sb.AppendLine("  BEGIN");
            sb.AppendLine("    IF @IsActive IS NULL");
            sb.AppendLine($"      SELECT * FROM [{table.Schema}].[{table.TableName}] WHERE [{primaryKeyColumn.ColumnName}] = @{primaryKeyColumn.ColumnName};");
            sb.AppendLine("    ELSE");
            sb.AppendLine($"      SELECT * FROM [{table.Schema}].[{table.TableName}] WHERE [{primaryKeyColumn.ColumnName}] = @{primaryKeyColumn.ColumnName} AND IsActive = @IsActive;");
            sb.AppendLine("  END");
            sb.Append("END");

            return sb.ToString();
        }

        public string GenerateGetByPrimaryKeyIdsProcedure(SqlTable table)
        {
            var primaryKeyConstraint = table.Constraints.FirstOrDefault(c => c.ConstraintType == Model.Enum.ConstraintType.PrimaryKey);
            var primaryKey = table.Columns.FirstOrDefault(s => s.ColumnName == primaryKeyConstraint.Column);

            var sb = new StringBuilder();

            sb.AppendLine($"/****** Object:  StoredProcedure [{table.Schema}].[zgen_{table.TableName}_GetBy{primaryKey.ColumnName}s] ******/");

            sb.AppendLine("-- ===================================================================");
            sb.AppendLine($"-- Description    : Select By Id {table.TableName}");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine();

            sb.AppendLine($"CREATE   PROCEDURE [{table.Schema}].[zgen_{table.TableName}_GetBy{primaryKey.ColumnName}s]");

            // Generate procedure parameters for the UDTT
            if (primaryKey.DataType == "INT")
            {
                sb.AppendLine("  (@Ids udtt_Ints READONLY, @IsActive bit=NULL)");
            }
            else if (primaryKey.DataType == "UNIQUEIDENTIFIER")
            {
                sb.AppendLine("  (@Ids udtt_Uniqueidentifiers READONLY, @IsActive BIT = NULL)");
            }
            else if (primaryKey.DataType == "TINYINT")
            {
                sb.AppendLine("  (@Ids udtt_TinyInts READONLY, @IsActive BIT = NULL)");
            }

            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("  SET NOCOUNT ON;");
            sb.AppendLine("  SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;");
            sb.AppendLine();

            sb.AppendLine("  IF @IsActive IS NULL");
            sb.AppendLine($"    SELECT * FROM [{table.Schema}].[{table.TableName}] WHERE [{primaryKey.ColumnName}] IN (Select Id FROM @Ids);");
            sb.AppendLine("  ELSE");
            sb.AppendLine($"    SELECT * FROM [{table.Schema}].[{table.TableName}] WHERE [{primaryKey.ColumnName}] IN (Select Id FROM @Ids) AND IsActive = @IsActive;");

            sb.Append("END");

            return sb.ToString();
        }

        public string GenerateGetByForeignKeyIdsProcedure(SqlTable table)
        {
            var sb = new StringBuilder();

            // Start the SQL procedure creation
            sb.AppendLine($"/****** Object:  StoredProcedure [{table.Schema}].[zgen_{table.TableName}_GetByIds] ******/");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine("-- Description    : Select By Ids " + table.TableName);
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine();

            sb.AppendLine($"CREATE   PROCEDURE [{table.Schema}].[zgen_{table.TableName}_GetByIds]");

            // Add parameters dynamically based on the foreign key columns
            sb.Append("  (");

            var sortedColumns = GetSortedColumnsByNullableDefaultType(table);
            var foreignColumns = sortedColumns.Where(col => IsForeignKey(col, table)).ToList();

            // Loop through only foreign key columns
            foreach (var column in foreignColumns)
                sb.Append($"@{column.ColumnName} {column.DataType}{(string.IsNullOrEmpty(column.DataTypeLength) ? "" : $"({column.DataTypeLength})")}=NULL,");

            sb.Append($"@IsActive bit=NULL,");
            sb.Append($"@SortDirection varchar(5)='ASC'");


            sb.AppendLine(")");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("  SET NOCOUNT ON;");
            sb.AppendLine("  SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;");
            sb.AppendLine();

            // Build the dynamic WHERE clause based on the foreign key parameters
            sb.AppendLine("  IF @IsActive IS NULL");
            sb.AppendLine("  BEGIN");
            sb.Append("    SELECT * FROM [" + table.Schema + "].[" + table.TableName + "] WHERE ");

            foreach (var column in foreignColumns)
                sb.Append($"(@{column.ColumnName} IS NULL OR [{column.ColumnName}] = @{column.ColumnName}) AND ");
            sb.Length -= 4;

            sb.AppendLine("");
            sb.AppendLine("    ORDER BY ");
            sb.AppendLine("    CASE WHEN @SortDirection = 'ASC' THEN CreateDT END ASC, CASE WHEN @SortDirection = 'DESC' THEN CreateDT END DESC");
            sb.AppendLine("    OPTION (RECOMPILE);");
            sb.AppendLine("  END");
            sb.AppendLine("  ELSE");
            sb.AppendLine("  BEGIN");
            sb.Append("    SELECT * FROM [" + table.Schema + "].[" + table.TableName + "] WHERE ");

            foreach (var column in foreignColumns)
                sb.Append($"(@{column.ColumnName} IS NULL OR [{column.ColumnName}] = @{column.ColumnName}) AND ");
            sb.Length -= 4;

            sb.AppendLine("");
            sb.AppendLine("    AND IsActive = @IsActive");
            sb.AppendLine("    ORDER BY ");
            sb.AppendLine("    CASE WHEN @SortDirection = 'ASC' THEN CreateDT END ASC, CASE WHEN @SortDirection = 'DESC' THEN CreateDT END DESC");
            sb.AppendLine("    OPTION (RECOMPILE);");
            sb.AppendLine("  END");



            sb.AppendLine("END");

            return sb.ToString();
        }

        public string GenerateGetByForeignKeyIdsPagingProcedure(SqlTable table)
        {
            var sb = new StringBuilder();

            var primaryColumn = GetPrimaryColumn(table);

            // Start the SQL procedure creation
            sb.AppendLine($"/****** Object:  StoredProcedure [{table.Schema}].[zgen_{table.TableName}_GetByIdsPaging] ******/");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine("-- Description    : Select By Ids Paging " + table.TableName);
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine();

            sb.AppendLine($"CREATE   PROCEDURE [{table.Schema}].[zgen_{table.TableName}_GetByIdsPaging]");

            var sortedColumns = GetSortedColumnsByNullableDefaultType(table);
            var foreignColumns = sortedColumns.Where(col => IsForeignKey(col, table)).ToList();

            sb.Append("  (");
            foreach (var column in foreignColumns)
                sb.Append($"@{column.ColumnName} {column.DataType}=NULL,");

            sb.Append($"@IsActive BIT=NULL,");
            sb.Append($"@PageNumber INT=1,");
            sb.Append($"@PageSize INT=50,");
            sb.Append($"@SortDirection VARCHAR(5)='ASC'");
            sb.AppendLine(")");

            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("  SET NOCOUNT ON;");
            sb.AppendLine("  SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;");
            sb.AppendLine();

            sb.AppendLine("  IF @IsActive IS NULL");
            sb.AppendLine("  BEGIN");
            sb.AppendLine($"    WITH CTE_{table.TableName} AS (");

            sb.Append($"    SELECT * FROM [{table.Schema}].[{table.TableName}] WHERE");

            // Add filtering conditions for foreign keys
            foreach (var column in foreignColumns)
                sb.Append($" (@{column.ColumnName} IS NULL OR [{column.ColumnName}] = @{column.ColumnName}) AND");

            sb.Length -= 3;
            sb.AppendLine();

            sb.AppendLine("    ORDER BY");
            sb.AppendLine("    CASE WHEN @SortDirection = 'ASC' THEN CreateDT END ASC, CASE WHEN @SortDirection = 'DESC' THEN CreateDT END DESC");
            sb.AppendLine($"    OFFSET @PageSize * (@PageNumber - 1) ROWS FETCH NEXT @PageSize ROWS ONLY),");

            sb.AppendLine("    CTE_TotalRows AS");
            sb.AppendLine("    (");
            sb.AppendLine($"      SELECT COUNT({primaryColumn.ColumnName}) AS TotalRows FROM [{table.Schema}].[{table.TableName}]");
            sb.Append("      WHERE");

            // Add filtering conditions for foreign keys again in the count query
            foreach (var column in foreignColumns)
                sb.Append($" (@{column.ColumnName} IS NULL OR [{column.ColumnName}] = @{column.ColumnName}) AND");

            sb.Length -= 3;
            sb.AppendLine();

            sb.AppendLine("    )");

            sb.AppendLine($"    SELECT TotalRows, [{table.Schema}].[{table.TableName}].* FROM [{table.Schema}].[{table.TableName}], CTE_TotalRows");
            sb.AppendLine($"    WHERE EXISTS (SELECT 1 FROM CTE_{table.TableName} WHERE CTE_{table.TableName}.{primaryColumn.ColumnName} = [{table.Schema}].[{table.TableName}].{primaryColumn.ColumnName})");
            sb.AppendLine("    ORDER BY");
            sb.AppendLine("    CASE WHEN @SortDirection = 'ASC' THEN CreateDT END ASC, CASE WHEN @SortDirection = 'DESC' THEN CreateDT END DESC");
            sb.AppendLine("    OPTION (RECOMPILE);");
            sb.AppendLine("  END");

            sb.AppendLine("  ELSE");
            sb.AppendLine("  BEGIN");
            sb.AppendLine($"    WITH CTE_{table.TableName} AS (");

            sb.Append($"    SELECT * FROM [{table.Schema}].[{table.TableName}] WHERE");

            // Add filtering conditions for foreign keys
            foreach (var column in foreignColumns)
                sb.Append($" (@{column.ColumnName} IS NULL OR [{column.ColumnName}] = @{column.ColumnName}) AND");
            sb.Length -= 3;
            sb.AppendLine("");
            sb.AppendLine(" AND IsActive = @IsActive");

            sb.AppendLine("    ORDER BY");
            sb.AppendLine("    CASE WHEN @SortDirection = 'ASC' THEN CreateDT END ASC, CASE WHEN @SortDirection = 'DESC' THEN CreateDT END DESC");
            sb.AppendLine($"    OFFSET @PageSize * (@PageNumber - 1) ROWS FETCH NEXT @PageSize ROWS ONLY),");

            sb.AppendLine("    CTE_TotalRows AS");
            sb.AppendLine("    (");
            sb.AppendLine($"      SELECT COUNT({primaryColumn.ColumnName}) AS TotalRows FROM [{table.Schema}].[{table.TableName}]");
            sb.Append("      WHERE");

            // Add filtering conditions for foreign keys again in the count query
            foreach (var column in foreignColumns)
                sb.Append($" (@{column.ColumnName} IS NULL OR [{column.ColumnName}] = @{column.ColumnName}) AND");
            sb.Length -= 3;

            sb.AppendLine();
            sb.AppendLine("      AND IsActive = @IsActive");
            sb.AppendLine("    )");

            sb.AppendLine($"    SELECT TotalRows, [{table.Schema}].[{table.TableName}].* FROM [{table.Schema}].[{table.TableName}], CTE_TotalRows");
            sb.AppendLine($"    WHERE EXISTS (SELECT 1 FROM CTE_{table.TableName} WHERE CTE_{table.TableName}.{primaryColumn.ColumnName} = [{table.Schema}].[{table.TableName}].{primaryColumn.ColumnName})");
            sb.AppendLine("    ORDER BY");
            sb.AppendLine("    CASE WHEN @SortDirection = 'ASC' THEN CreateDT END ASC, CASE WHEN @SortDirection = 'DESC' THEN CreateDT END DESC");
            sb.AppendLine("    OPTION (RECOMPILE);");
            sb.AppendLine("  END");

            sb.AppendLine("END");

            return sb.ToString();
        }

        public List<string> GenerateGetByIndexedColumnProcedures(SqlTable table)
        {
            var procedures = new List<string>();

            // Loop through each index for the table
            foreach (var index in table.Indexes)
            {
                var column = table.Columns.First(s => s.ColumnName == index.Column);
                var sb = new StringBuilder();

                sb.AppendLine($"/****** Object:  StoredProcedure [{table.Schema}].[zgen_{table.TableName}_GetBy{index.Column}] ******/");
                sb.AppendLine("-- ===================================================================");
                sb.AppendLine($"-- Description    : Select By {index.Column} {table.TableName}");
                sb.AppendLine("-- ===================================================================");
                sb.AppendLine();

                sb.AppendLine($"CREATE   PROCEDURE [{table.Schema}].[zgen_{table.TableName}_GetBy{index.Column}]");

                // Add parameter based on the indexed column
                sb.AppendLine($"  (@{column.ColumnName} {column.DataType.ToLower()}{(!string.IsNullOrEmpty(column.DataTypeLength) ? $"({column.DataTypeLength})" : "")})");

                sb.AppendLine("AS");
                sb.AppendLine("BEGIN");
                sb.AppendLine("  SET NOCOUNT ON;");
                sb.AppendLine("  SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;");
                sb.AppendLine();
                sb.AppendLine($" SELECT * FROM [{table.Schema}].[{table.TableName}] WHERE {index.Column} = @{index.Column}");
                sb.Append("END");

                // Add the generated procedure string to the list
                procedures.Add(sb.ToString());
            }

            return procedures;
        }

        public string GenerateDelHrdProcedure(SqlTable table)
        {
            var sb = new StringBuilder();

            // Get the primary key column from the table constraints
            var primaryKeyConstraint = table.Constraints
                                        .FirstOrDefault(c => c.ConstraintType == Model.Enum.ConstraintType.PrimaryKey);

            var primaryKey = table.Columns.First(s => s.ColumnName == primaryKeyConstraint.Column);

            if (primaryKeyConstraint == null)
            {
                throw new Exception("Table does not have a primary key constraint.");
            }

            sb.AppendLine($"/****** Object:  StoredProcedure [{table.Schema}].[zgen_{table.TableName}_DelHrd] ******/");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine($"-- Description    : Hard Delete {table.TableName}");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine();
            sb.AppendLine($"CREATE   PROCEDURE [{table.Schema}].[zgen_{table.TableName}_DelHrd]");
            sb.AppendLine($"  (@{primaryKeyConstraint.Column} {primaryKey.DataType.ToLower()}, @RetMsg NVARCHAR(MAX) OUTPUT)");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("  DECLARE @InitialTransCount INT = @@TRANCOUNT;");
            sb.AppendLine("  DECLARE @TranName varchar(32) = OBJECT_NAME(@@PROCID);");
            sb.AppendLine();
            sb.AppendLine("  BEGIN TRY");
            sb.AppendLine("    IF @InitialTransCount = 0 BEGIN TRANSACTION @TranName");
            sb.AppendLine();
            sb.AppendLine($"    DELETE FROM [{table.Schema}].[{table.TableName}] WHERE [{primaryKeyConstraint.Column}]=@{primaryKeyConstraint.Column};");
            sb.AppendLine();
            sb.AppendLine("    IF @@ERROR <> 0 BEGIN GOTO errorMsg_section END");
            sb.AppendLine();
            sb.AppendLine("    IF @InitialTransCount = 0 COMMIT TRANSACTION @TranName");
            sb.AppendLine("    SET @RetMsg = LTRIM(ISNULL(@RetMsg, '') + 'Successful')");
            sb.AppendLine("    RETURN 0");
            sb.AppendLine();
            sb.AppendLine("  errorMsg_section:");
            sb.AppendLine("    SET @RetMsg = LTRIM(ISNULL(@RetMsg, '') + ' SQLErrMSG: ' + ISNULL(ERROR_MESSAGE(), ''))");
            sb.AppendLine("    GOTO error_section");
            sb.AppendLine();
            sb.AppendLine("  error_section:");
            sb.AppendLine("    SET @RetMsg = ISNULL(@RetMsg, '')");
            sb.AppendLine("    IF @InitialTransCount = 0 ROLLBACK TRANSACTION @TranName");
            sb.AppendLine("    RETURN 1");
            sb.AppendLine("  END TRY");
            sb.AppendLine("  BEGIN CATCH");
            sb.AppendLine("    IF @InitialTransCount = 0 ROLLBACK TRANSACTION @TranName");
            sb.AppendLine("    SET @RetMsg = LTRIM(ISNULL(@RetMsg, '') + ' SQLErrMSG: ' + ISNULL(ERROR_MESSAGE(), ''))");
            sb.AppendLine("    RETURN 1");
            sb.AppendLine("  END CATCH");
            sb.Append("END");

            return sb.ToString();
        }

        public string GenerateDelSftProcedure(SqlTable table)
        {
            var sb = new StringBuilder();

            // Get the primary key column from the table constraints
            var primaryKeyConstraint = table.Constraints
                                        .FirstOrDefault(c => c.ConstraintType == Model.Enum.ConstraintType.PrimaryKey);

            var primaryKey = table.Columns.First(s => s.ColumnName == primaryKeyConstraint.Column);

            if (primaryKeyConstraint == null)
            {
                throw new Exception("Table does not have a primary key constraint.");
            }

            sb.AppendLine($"/****** Object:  StoredProcedure [{table.Schema}].[zgen_{table.TableName}_DelSft] ******/");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine($"-- Description    : Soft Delete {table.TableName}");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine();
            sb.AppendLine($"CREATE   PROCEDURE [{table.Schema}].[zgen_{table.TableName}_DelSft]");
            sb.AppendLine($"  (@{primaryKeyConstraint.Column} {primaryKey.DataType.ToLower()}, @RetMsg NVARCHAR(MAX) OUTPUT)");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("  DECLARE @InitialTransCount INT = @@TRANCOUNT;");
            sb.AppendLine("  DECLARE @TranName varchar(32) = OBJECT_NAME(@@PROCID);");
            sb.AppendLine();
            sb.AppendLine("  BEGIN TRY");
            sb.AppendLine("    IF @InitialTransCount = 0 BEGIN TRANSACTION @TranName");
            sb.AppendLine();
            sb.AppendLine($"    UPDATE [{table.Schema}].[{table.TableName}] SET IsActive = 0 WHERE [{primaryKeyConstraint.Column}]=@{primaryKeyConstraint.Column};");
            sb.AppendLine();
            sb.AppendLine("    IF @@ERROR <> 0 BEGIN GOTO errorMsg_section END");
            sb.AppendLine();
            sb.AppendLine("    IF @InitialTransCount = 0 COMMIT TRANSACTION @TranName");
            sb.AppendLine("    SET @RetMsg = LTRIM(ISNULL(@RetMsg, '') + 'Successful')");
            sb.AppendLine("    RETURN 0");
            sb.AppendLine();
            sb.AppendLine("  errorMsg_section:");
            sb.AppendLine("    SET @RetMsg = LTRIM(ISNULL(@RetMsg, '') + ' SQLErrMSG: ' + ISNULL(ERROR_MESSAGE(), ''))");
            sb.AppendLine("    GOTO error_section");
            sb.AppendLine();
            sb.AppendLine("  error_section:");
            sb.AppendLine("    SET @RetMsg = ISNULL(@RetMsg, '')");
            sb.AppendLine("    IF @InitialTransCount = 0 ROLLBACK TRANSACTION @TranName");
            sb.AppendLine("    RETURN 1");
            sb.AppendLine("  END TRY");
            sb.AppendLine("  BEGIN CATCH");
            sb.AppendLine("    IF @InitialTransCount = 0 ROLLBACK TRANSACTION @TranName");
            sb.AppendLine("    SET @RetMsg = LTRIM(ISNULL(@RetMsg, '') + ' SQLErrMSG: ' + ISNULL(ERROR_MESSAGE(), ''))");
            sb.AppendLine("    RETURN 1");
            sb.AppendLine("  END CATCH");
            sb.Append("END");

            return sb.ToString();
        }

        public string GenerateSearchProcedure(SqlTable sqlTable,string sqlStoredProcedurePath, SqlStoredProcedure sqlStoredProcedure)
        {
            var sb = new StringBuilder();
            var primaryColumn = GetPrimaryColumn(sqlTable);
            bool referenceTableExists = false;
            sb.AppendLine($"/****** Object:  StoredProcedure [{sqlTable.Schema}].[{sqlStoredProcedure.StoredProcedureName}] ******/");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine($"-- Description     : Search {sqlTable.TableName} based on provided filters.");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine();

            sb.AppendLine($"CREATE PROCEDURE [{sqlTable.Schema}].[{sqlStoredProcedure.StoredProcedureName}]");
            sb.AppendLine("(");

            var parameters = sqlStoredProcedure.Parameters;

            var standardParams = new[] {"SortDirection", "PageNumber", "PageSize" };
            var existingStandardParams = parameters.Select(p => p.ColumnName.ToLower()).ToList();

            var filteredParams = parameters
                .Where(p => !standardParams.Contains(p.ColumnName, StringComparer.OrdinalIgnoreCase))
                .ToList();


            foreach (var param in parameters)
            {
                if (param.ColumnName.ToLower().Equals("referencenumber", StringComparison.OrdinalIgnoreCase))
                {
                    var referencetableName = $"{sqlTable.TableName}Reference";
                    var directory = Directory.GetParent(Path.GetDirectoryName(sqlStoredProcedurePath));
                    var tablePath = Path.Combine(directory.FullName, "Tables", $"{referencetableName}.sql");

                    if (File.Exists(tablePath))
                    {
                        referenceTableExists = true;
                    }
                }

                sb.Append($"    @{param.ColumnName} {param.DataType}");

                if (param.DataType.ToLower().Contains("udtt_"))
                {
                    sb.Append(" READONLY");
                }
                else if (!string.IsNullOrEmpty(param.DataTypeLength) &&
                         (param.DataType.ToLower().StartsWith("varchar") || param.DataType.ToLower().StartsWith("nvarchar")))
                {
                    sb.Append($"({param.DataTypeLength})");
                }

                if (!string.IsNullOrEmpty(param.DefaultValue))
                {
                    sb.Append($" = '{param.DefaultValue}'");
                }
             

                sb.AppendLine(",");
            }

           
            if (!existingStandardParams.Contains("sortdirection"))
                sb.AppendLine("    @SortDirection VARCHAR(5) = 'DESC',");
            if (!existingStandardParams.Contains("pagenumber"))
                sb.AppendLine("    @PageNumber INT = 1,");
            if (!existingStandardParams.Contains("pagesize"))
                sb.AppendLine("    @PageSize INT = 50");
            if (!existingStandardParams.Contains("isactive"))
                sb.AppendLine("    @IsActive BIT = 1");

            sb.Length -= 3; // Trim last comma
            sb.AppendLine();
            sb.AppendLine(")");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    SET NOCOUNT ON;");
            sb.AppendLine("    SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;");
            sb.AppendLine();

            sb.AppendLine($"    WITH CTE_{sqlTable.TableName} AS (");
            sb.AppendLine($"        SELECT T.*");
            sb.AppendLine($"        FROM [{sqlTable.Schema}].[{sqlTable.TableName}] T");
            if (referenceTableExists)
            {
                sb.AppendLine($"        INNER JOIN [{sqlTable.Schema}].[{sqlTable.TableName}Reference] RT ON T.[{sqlTable.TableName}Id] = RT.[{sqlTable.TableName}ReferenceId]  ");

            }
            sb.AppendLine("        WHERE ");

            // Check if the stored procedure name contains 'Date' and apply date filters as a combined condition
            if (sqlStoredProcedure.StoredProcedureName.Contains("Date", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"             T.CreateDT BETWEEN @FromDateTime AND @ToDateTime AND");
            }
            var filterParams = parameters
                .Where(p => !standardParams.Contains(p.ColumnName) &&
                            !p.ColumnName.Equals("FromDateTime", StringComparison.OrdinalIgnoreCase) &&
                            !p.ColumnName.Equals("ToDateTime", StringComparison.OrdinalIgnoreCase))
                .ToList();

            for (int i = 0; i < filterParams.Count; i++)
            {
                var param = filterParams[i];
           
                bool isLast = (i == filterParams.Count - 1);

                if (param.DataType.ToLower().Contains("udtt_"))
                {
                    var foreignKeyColumn = GetForeignKeyColumnName(sqlTable, param.ColumnName);
                    sb.Append($"             T.{foreignKeyColumn} IN (SELECT [Id] FROM @{param.ColumnName})");
                }
                else
                {
                    if (referenceTableExists && param.ColumnName.ToLower().Equals("referencenumber"))
                    {
                        sb.Append($"             RT.{sqlTable.TableName}ReferenceNumber = @{param.ColumnName}");

                    }
                    else
                    {
                        sb.Append($"             T.{param.ColumnName} = @{param.ColumnName}");

                    }



                }

                if (!isLast)
                    sb.AppendLine(" AND");
              
            }

            sb.AppendLine("        ORDER BY");
            sb.AppendLine($"            CASE WHEN @SortDirection = 'ASC' THEN T.[CreateDT] END ASC,");
            sb.AppendLine($"            CASE WHEN @SortDirection = 'DESC' THEN T.[CreateDT] END DESC");
            sb.AppendLine("        OFFSET @PageSize * (@PageNumber - 1) ROWS");
            sb.AppendLine("        FETCH NEXT @PageSize ROWS ONLY");
            sb.AppendLine("    ),");

            sb.AppendLine("    CTE_TotalRows AS (");
            sb.AppendLine($"        SELECT COUNT({primaryColumn?.ColumnName ?? "1"}) AS TotalRows");
            sb.AppendLine($"        FROM [{sqlTable.Schema}].[{sqlTable.TableName}] T");
            sb.AppendLine("        WHERE ");

            // Reapply the same date filter for total rows query
            if (sqlStoredProcedure.StoredProcedureName.Contains("Date", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"             T.CreateDT BETWEEN @FromDateTime AND @ToDateTime AND");
            }

          

            for (int i = 0; i < filterParams.Count; i++)
            {
                var param = filterParams[i];
                bool isLast = (i == filterParams.Count - 1);

                if (param.DataType.ToLower().Contains("udtt_"))
                {
                    var foreignKeyColumn = GetForeignKeyColumnName(sqlTable, param.ColumnName);
                    sb.Append($"             T.{foreignKeyColumn} IN (SELECT [Id] FROM @{param.ColumnName})");
                }
                else
                {
                    if (referenceTableExists && param.ColumnName.ToLower().Equals("referencenumber"))
                    {
                        sb.Append($"             RT.{sqlTable.TableName}ReferenceNumber = @{param.ColumnName}");

                    }
                    else
                    {
                        sb.Append($"             T.{param.ColumnName} = @{param.ColumnName}");

                    }
                }

                if (!isLast)
                    sb.AppendLine(" AND");

            }

            sb.AppendLine("    )");
            sb.AppendLine();
            sb.AppendLine("    SELECT");
            if (referenceTableExists)
            {
                sb.AppendLine("        RT.*,");
                sb.AppendLine("        TR.TotalRows ");
                sb.AppendLine($"    FROM CTE_{sqlTable.TableName} T");
                sb.AppendLine($"        INNER JOIN [{sqlTable.Schema}].[{sqlTable.TableName}Reference] RT ON T.[{sqlTable.TableName}Id] = RT.[{sqlTable.TableName}ReferenceId]  ");
                sb.AppendLine("    CROSS JOIN CTE_TotalRows TR");
            }
            else
            {
                sb.AppendLine("        T.*,");
                sb.AppendLine("        TR.TotalRows");
                sb.AppendLine($"    FROM CTE_{sqlTable.TableName} T");
                sb.AppendLine("    CROSS JOIN CTE_TotalRows TR");
            }

            sb.AppendLine("        ORDER BY");
            sb.AppendLine($"            CASE WHEN @SortDirection = 'ASC' THEN T.[CreateDT] END ASC,");
            sb.AppendLine($"            CASE WHEN @SortDirection = 'DESC' THEN T.[CreateDT] END DESC");
            sb.AppendLine("        OFFSET @PageSize * (@PageNumber - 1) ROWS");
            sb.AppendLine("        FETCH NEXT @PageSize ROWS ONLY;");
            sb.AppendLine("END");

            return sb.ToString();
        }


        private string GetForeignKeyColumnName(SqlTable table, string parameterName)
        {
            // Simple heuristic: remove "Ids" suffix and check if a column with that name exists.
            var baseName = parameterName.Replace("Ids", "", StringComparison.OrdinalIgnoreCase);
            return table.Columns.Any(c => c.ColumnName.Equals(baseName + "Id", StringComparison.OrdinalIgnoreCase)) ? baseName + "Id" :
                   table.Columns.FirstOrDefault(c => c.ColumnName.Equals(baseName, StringComparison.OrdinalIgnoreCase))?.ColumnName;
            // You might need more sophisticated logic here based on your naming conventions.
        }

        private string SanitizeSortColumn(string sortColumn, SqlTable table)
        {
            // Basic sanitization to prevent SQL injection (can be improved)
            if (string.IsNullOrEmpty(sortColumn))
            {
                return "CreateDT"; // Default
            }
            return table.Columns.Any(c => c.ColumnName.Equals(sortColumn, StringComparison.OrdinalIgnoreCase)) ? sortColumn : "CreateDT";
        }
        private bool IsForeignKey(SqlColumn column, SqlTable table)
        {
            // Check if the column is part of a foreign key constraint (you'll need to adjust this based on how foreign keys are represented in your data)
            return table.Constraints.Any(c => c.ConstraintType == Model.Enum.ConstraintType.ForeignKey && c.Column == column.ColumnName);
        }

        private SqlColumn GetPrimaryColumn(SqlTable table)
        {
            // Identify the primary key column
            var primaryKeyColumn = table.Constraints.FirstOrDefault(c => c.ConstraintType == Model.Enum.ConstraintType.PrimaryKey)?.Column;
            if (primaryKeyColumn == null)
                throw new InvalidOperationException("No primary key column found for table " + table.TableName);

            // Get primary key column details
            var column = table.Columns.First(c => c.ColumnName == primaryKeyColumn);
            return column;
        }

        private List<SqlColumn> GetSortedColumnsByNullableDefaultType(SqlTable table)
        {
            Dictionary<string, string> columnDescriptions = new Dictionary<string, string>();
            Dictionary<string, int> columnIndexes = new Dictionary<string, int>();
            var primaryKey = table.Constraints.First(con => con.ConstraintType == Model.Enum.ConstraintType.PrimaryKey);
            int i = 0;
            table.Columns.ForEach(s =>
            {
                columnIndexes[s.ColumnName] = i++;
                columnDescriptions[s.ColumnName] = "3_NULLABLE";

                if (s.ColumnName.Equals(primaryKey.Column))
                    columnDescriptions[s.ColumnName] = "3_NULLABLE";
                else if (!s.IsNullable)
                {

                    if (string.IsNullOrWhiteSpace(s.DefaultValue))
                        columnDescriptions[s.ColumnName] = "1_NOT_NULLABLE_W_DEFAULT";
                    else
                        columnDescriptions[s.ColumnName] = "2_NOT_NULLABLE_NO_DEFAULT";
                }

                if (s.ColumnName == "IsActive")
                    columnDescriptions[s.ColumnName] = "4_LAST";
            });

            return table.Columns.OrderBy(s => columnDescriptions[s.ColumnName])
                                .ThenBy(s => columnIndexes[s.ColumnName])
                                .ToList();
        }
    }
}
