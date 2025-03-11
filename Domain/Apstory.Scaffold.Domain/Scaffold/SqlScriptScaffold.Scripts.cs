using Apstory.Scaffold.Model.Sql;
using System.Text;

namespace Apstory.Scaffold.Domain.Scaffold
{
    public partial class SqlScriptFileScaffold
    {
        private string[] skipDTDefaults = new string[] { "CreateDT", "UpdateDT" };

        public string GenerateInsertUpdateProcedure(SqlTable table)
        {
            var sb = new StringBuilder();

            var primaryColumn = GetPrimaryColumn(table);

            // Start the SQL procedure creation
            sb.AppendLine($"/****** Object:  StoredProcedure [dbo].[zgen_{table.TableName}_InsUpd] ******/");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine("-- Description    : Insert Update " + table.TableName);
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine();

            sb.AppendLine($"CREATE   PROCEDURE [dbo].[zgen_{table.TableName}_InsUpd]");

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

            sb.AppendLine($"    IF @{primaryColumn.ColumnName} IS NULL");
            sb.AppendLine("    BEGIN");
            if (!primaryColumn.DataType.Equals("UNIQUEIDENTIFIER", StringComparison.OrdinalIgnoreCase))
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
            sb.AppendLine($"/****** Object:  StoredProcedure [dbo].[zgen_{table.TableName}_GetById] ******/");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine("-- Description    : Select By Id " + table.TableName);
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine();

            sb.AppendLine($"CREATE   PROCEDURE [dbo].[zgen_{table.TableName}_GetById]");

            var primaryKeyColumn = GetPrimaryColumn(table);

            // Add parameters
            sb.AppendLine($"  (@{primaryKeyColumn.ColumnName} {primaryKeyColumn.DataType.ToLower()}, @IsActive bit=NULL)");

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

            sb.AppendLine($"/****** Object:  StoredProcedure [dbo].[zgen_{table.TableName}_GetBy{primaryKey.ColumnName}s] ******/");

            sb.AppendLine("-- ===================================================================");
            sb.AppendLine($"-- Description    : Select By Id {table.TableName}");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine();

            sb.AppendLine($"CREATE   PROCEDURE [dbo].[zgen_{table.TableName}_GetBy{primaryKey.ColumnName}s]");

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
            sb.AppendLine($"/****** Object:  StoredProcedure [dbo].[zgen_{table.TableName}_GetByIds] ******/");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine("-- Description    : Select By Ids " + table.TableName);
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine();

            sb.AppendLine($"CREATE   PROCEDURE [dbo].[zgen_{table.TableName}_GetByIds]");

            // Add parameters dynamically based on the foreign key columns
            sb.Append("  (");

            var sortedColumns = GetSortedColumnsByNullableDefaultType(table);
            var foreignColumns = sortedColumns.Where(col => IsForeignKey(col, table)).ToList();

            // Loop through only foreign key columns
            foreach (var column in foreignColumns)
                sb.Append($"@{column.ColumnName} {column.DataType.ToLower()}=NULL,");

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
            sb.AppendLine($"/****** Object:  StoredProcedure [dbo].[zgen_{table.TableName}_GetByIdsPaging] ******/");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine("-- Description    : Select By Ids Paging " + table.TableName);
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine();

            sb.AppendLine($"CREATE   PROCEDURE [dbo].[zgen_{table.TableName}_GetByIdsPaging]");

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

                sb.AppendLine($"/****** Object:  StoredProcedure [dbo].[zgen_{table.TableName}_GetBy{index.Column}] ******/");
                sb.AppendLine("-- ===================================================================");
                sb.AppendLine($"-- Description    : Select By {index.Column} {table.TableName}");
                sb.AppendLine("-- ===================================================================");
                sb.AppendLine();

                sb.AppendLine($"CREATE   PROCEDURE [dbo].[zgen_{table.TableName}_GetBy{index.Column}]");

                // Add parameter based on the indexed column
                sb.AppendLine($"  (@{column.ColumnName} {column.DataType.ToLower()}{(column.DataTypeLength is not null ? $"({column.DataTypeLength})" : "")}=NULL)");

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

            sb.AppendLine($"/****** Object:  StoredProcedure [dbo].[zgen_{table.TableName}_DelHrd] ******/");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine($"-- Description    : Hard Delete {table.TableName}");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine();
            sb.AppendLine($"CREATE   PROCEDURE [dbo].[zgen_{table.TableName}_DelHrd]");
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

            sb.AppendLine($"/****** Object:  StoredProcedure [dbo].[zgen_{table.TableName}_DelSft] ******/");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine($"-- Description    : Soft Delete {table.TableName}");
            sb.AppendLine("-- ===================================================================");
            sb.AppendLine();
            sb.AppendLine($"CREATE   PROCEDURE [dbo].[zgen_{table.TableName}_DelSft]");
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
