using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Typescript;
using System.Text;

namespace Apstory.Scaffold.Domain.Scaffold
{
    public partial class SqlLiteScaffold
    {
        private void GenerateHeader(StringBuilder sb, TSModel tsModel)
        {
            sb.AppendLine("import { Injectable } from '@angular/core';");
            sb.AppendLine("import { SQLiteService } from '../../storage/sqlite.service';");
            sb.AppendLine("import { BaseDalService } from './base-dal.service';");
            sb.AppendLine($"import {{ {tsModel.TSModelName} }} from 'src/app/models/gen/{tsModel.TSModelName.ToLower()}';");
            sb.AppendLine("import { LoggerService } from '../../logger.service';");
            sb.AppendLine("import { DBSQLiteValues } from '@capacitor-community/sqlite';");
            sb.AppendLine();
            sb.AppendLine("@Injectable()");
            sb.AppendLine($"export class {tsModel.TSModelName}DalService extends BaseDalService<{tsModel.TSModelName}> {{");
            sb.AppendLine($"  private TABLE_NAME = '{tsModel.TSModelName}';");
            sb.AppendLine();
            sb.AppendLine("  constructor(sqliteService: SQLiteService, logger: LoggerService) { ");
            sb.AppendLine("    super(sqliteService, logger);");
            sb.AppendLine("  }");
        
        }

        private void GenerateSqliteCreate(StringBuilder sb, TSModel tsModel)
        {
            try
            {

                sb.AppendLine($"  private async create{tsModel.TSModelName}() {{");
                sb.AppendLine($"    try {{");

                StringBuilder sbParams = new StringBuilder();
                foreach (var parm in tsModel.Properties)
                    sbParams.Append($"{(sbParams.Length > 0 ? ", " : "")}{parm.PropertyName.ToCamelCase()} {GetSqliteType(parm.PropertyType)}{(IsPrimaryKey(tsModel, parm) ? " PRIMARY KEY" : "")}");

                sb.AppendLine($"      await this.OpenDB();");
                sb.AppendLine($"      await this.db.run('CREATE TABLE IF NOT EXISTS {tsModel.TSModelName}({sbParams.ToString()})');");
                sb.AppendLine($"    }} catch (error) {{");
                sb.AppendLine($"      await this.handleError(error);");
                sb.AppendLine($"      return undefined;");
                sb.AppendLine($"    }}");
                sb.AppendLine($"  }}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(ex.Message);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private void GenerateSqliteAddReplace(StringBuilder sb, TSModel tsModel)
        {
            try
            {
                Console.WriteLine($"Process sqlite addreplace for table {tsModel.TSModelName}");
                sb.AppendLine();
                sb.AppendLine($"  public async AddReplace(entity: {tsModel.TSModelName}): Promise<boolean> {{");
                sb.AppendLine($"    try {{");
                sb.AppendLine($"      if (!entity.{tsModel.PrimaryKey.PropertyName}) {{");
                sb.AppendLine($"        entity.{tsModel.PrimaryKey.PropertyName} = BaseDalService.GenerateUniqueGuid();");
                sb.AppendLine($"      }}");

                StringBuilder qMarks = new StringBuilder();
                StringBuilder sbParams = new StringBuilder();
                foreach (var parm in tsModel.Properties)
                {
                    qMarks.Append($"{(qMarks.Length > 0 ? ", " : "")}?");
                    if (parm.PropertyType.Equals("bit", StringComparison.InvariantCultureIgnoreCase) || parm.PropertyType.Equals("boolean", StringComparison.InvariantCultureIgnoreCase))
                        sbParams.Append($"{(sbParams.Length > 0 ? ", " : "")}Number(entity.{parm.PropertyName.ToCamelCase()})");
                    else if (parm.PropertyType.Equals("Date", StringComparison.InvariantCultureIgnoreCase))
                        sbParams.Append($"{(sbParams.Length > 0 ? ", " : "")}entity.{parm.PropertyName.ToCamelCase()}?.toISOString()");
                    else
                        sbParams.Append($"{(sbParams.Length > 0 ? ", " : "")}entity.{parm.PropertyName.ToCamelCase()}");
                }

                sb.AppendLine($"      await this.OpenDB();");
                sb.AppendLine($"      await this.db.run('REPLACE INTO {tsModel.TSModelName} VALUES ({qMarks})', [{sbParams}]);");
                sb.AppendLine($"      await this.db.close();");
                sb.AppendLine($"      return true;");
                sb.AppendLine($"    }} catch (error) {{");
                sb.AppendLine($"      await this.handleError(error);");
                sb.AppendLine($"      return false;");
                sb.AppendLine($"    }}");
                sb.AppendLine($"  }}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(ex.Message);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private void GenerateSqliteGetById(StringBuilder sb, TSModel tsModel)
        {
            try
            {
                Console.WriteLine("Process sqlite getById for table " + tsModel.TSModelName);
                sb.AppendLine();
                sb.AppendLine($"  public async Get{tsModel.TSModelName}By{tsModel.PrimaryKey.PropertyName.Replace(tsModel.TSModelName, "")}({tsModel.PrimaryKey.PropertyName.ToCamelCase()}: {tsModel.PrimaryKey.PropertyType}) {{");
                sb.AppendLine($"    try {{");
                sb.AppendLine($"      await this.OpenDB();");
                sb.AppendLine($"      let result = await this.db.query('SELECT * FROM {tsModel.TSModelName} WHERE {tsModel.PrimaryKey.PropertyName} = ?', [{tsModel.PrimaryKey.PropertyName.ToCamelCase()}]);");

                if (tsModel.Properties.Any(s => s.PropertyType.Equals("date", StringComparison.InvariantCultureIgnoreCase)))
                    sb.AppendLine($"      this.ParseDates(result.values as Array<{tsModel.TSModelName}>);");

                sb.AppendLine($"      return (result.values as Array<{tsModel.TSModelName}>)[0];");
                sb.AppendLine($"    }} catch (error) {{");
                sb.AppendLine($"      await this.handleError(error);");
                sb.AppendLine($"      return undefined;");
                sb.AppendLine($"    }}");
                sb.AppendLine($"  }}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(ex.Message);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private void GenerateSqliteGetAll(StringBuilder sb, TSModel tsModel)
        {
            try
            {
                Console.WriteLine($"Process sqlite get for table {tsModel.TSModelName}");
                sb.AppendLine();
                sb.AppendLine($"  public async Get{tsModel.TSModelName}s() {{");
                sb.AppendLine($"    try {{");
                sb.AppendLine($"      await this.OpenDB();");
                sb.AppendLine($"      let result = await this.db.query('SELECT * FROM {tsModel.TSModelName}');");

                if (tsModel.Properties.Any(s => s.PropertyType.Equals("date", StringComparison.InvariantCultureIgnoreCase)))
                    sb.AppendLine($"      this.ParseDates(result.values as Array<{tsModel.TSModelName}>);");

                sb.AppendLine($"      return result.values as Array<{tsModel.TSModelName}>;");
                sb.AppendLine($"    }} catch (error) {{");
                sb.AppendLine($"      await this.handleError(error);");
                sb.AppendLine($"      return undefined;");
                sb.AppendLine($"    }}");
                sb.AppendLine($"  }}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(ex.Message);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private void GenerateSqliteCount(StringBuilder sb, TSModel tsModel)
        {
            try
            {
                Console.WriteLine($"Process sqlite count for table {tsModel.TSModelName}");
                sb.AppendLine();
                sb.AppendLine($"  public override async Count(isSynced: boolean): Promise<number | undefined> {{");
                sb.AppendLine($"    try {{");
                sb.AppendLine($"      await this.OpenDB();");
                sb.AppendLine($"      let result = await this.db.query('SELECT COUNT(*) as totalRows FROM {tsModel.TSModelName} WHERE IsActive = 1 and IsSynced = ?', [Number(isSynced)]);");
                sb.AppendLine($"      return (result.values as Array<{tsModel.TSModelName}>)[0].totalRows;");
                sb.AppendLine($"    }} catch (error) {{");
                sb.AppendLine($"      await this.handleError(error);");
                sb.AppendLine($"      return undefined;");
                sb.AppendLine($"    }}");
                sb.AppendLine($"  }}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(ex.Message);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private void GenerateSqliteParseDates(StringBuilder sb, TSModel tsModel)
        {
            try
            {
                var dateProperties = tsModel.Properties.Where(s => s.PropertyType.Equals("date", StringComparison.InvariantCultureIgnoreCase));
                if (!dateProperties.Any())
                    return;

                sb.AppendLine();
                sb.AppendLine($"  protected ParseDates({tsModel.TSModelName.ToCamelCase()}: Array<{tsModel.TSModelName}>) {{");
                sb.AppendLine($"    {tsModel.TSModelName.ToCamelCase()}.forEach(i => {{");

                StringBuilder sbParams = new StringBuilder();
                foreach (var parm in dateProperties)
                    sb.AppendLine($"      if (i.{parm.PropertyName.ToCamelCase()}) {{ i.{parm.PropertyName.ToCamelCase()} = new Date(i.{parm.PropertyName.ToCamelCase()}); }}");

                sb.AppendLine($"    }});");
                sb.AppendLine($"  }}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(ex.Message);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private void GenerateSqliteGetByIds(StringBuilder sb, TSModel tsModel)
        {
            try
            {
                foreach (var key in tsModel.ForeignKeys)
                {
                    var propertyName = key.Key;
                    var propertyType = key.Value;

                    Console.WriteLine($"Process sqlite getByIds for table {tsModel.TSModelName}");
                    sb.AppendLine();
                    sb.AppendLine($"  public async Get{tsModel.TSModelName}sBy{propertyName}IsSynced({propertyName.ToCamelCase()}: {propertyType}, isSynced: boolean | undefined) {{");
                    sb.AppendLine($"    try {{");
                    sb.AppendLine($"      await this.OpenDB();");
                    sb.AppendLine($"      let result: DBSQLiteValues;");
                    sb.AppendLine($"      if (typeof isSynced === 'undefined') {{");
                    sb.AppendLine($"        result = await this.db.query('SELECT * FROM {tsModel.TSModelName} WHERE {propertyName} = ?', [{propertyName.ToCamelCase()}]);");
                    sb.AppendLine($"      }} else {{");
                    sb.AppendLine($"        result = await this.db.query('SELECT * FROM {tsModel.TSModelName} WHERE {propertyName} = ? AND isSynced = ?', [{propertyName.ToCamelCase()}, Number(isSynced)]);");
                    sb.AppendLine($"      }};");

                    if (tsModel.Properties.Any(s => s.PropertyType.Equals("date", StringComparison.InvariantCultureIgnoreCase)))
                        sb.AppendLine($"      this.ParseDates(result.values as Array<{tsModel.TSModelName}>);");

                    sb.AppendLine($"      return result.values as Array<{tsModel.TSModelName}>;");
                    sb.AppendLine($"    }} catch (error) {{");
                    sb.AppendLine($"      await this.handleError(error);");
                    sb.AppendLine($"      return undefined;");
                    sb.AppendLine($"    }}");
                    sb.AppendLine($"  }}");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(ex.Message);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private void GenerateSqliteGetCountByIds(StringBuilder sb, TSModel tsModel)
        {
            try
            {
                foreach (var key in tsModel.ForeignKeys)
                {
                    var propertyName = key.Key;
                    var propertyType = key.Value;

                    Console.WriteLine("Process sqlite getCountByIds for table " + tsModel.TSModelName);
                    sb.AppendLine();
                    sb.AppendLine($"  public async CountBy{propertyName}({propertyName.ToCamelCase()}: {propertyType}, isSynced: boolean) {{");
                    sb.AppendLine($"    try {{");
                    sb.AppendLine($"      await this.OpenDB();");
                    sb.AppendLine($"      let result = await this.db.query('SELECT COUNT(*) as totalRows FROM {tsModel.TSModelName} WHERE {propertyName} = ? AND isSynced = ?', [{propertyName.ToCamelCase()}, Number(isSynced)]);");
                    sb.AppendLine($"      return (result.values as Array<{tsModel.TSModelName}>)[0].totalRows;");
                    sb.AppendLine($"    }} catch (error) {{");
                    sb.AppendLine($"      await this.handleError(error);");
                    sb.AppendLine($"      return undefined;");
                    sb.AppendLine($"    }}");
                    sb.AppendLine($"  }}");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(ex.Message);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }


        private static string GetSqliteType(string propertyType)
        {
            switch (propertyType.ToLower())
            {
                case "number":
                    return "INTEGER";
                case "string":
                    return "NVARCHAR";
                case "bit":
                case "boolean":
                    return "INTEGER DEFAULT 1";
                case "date":
                case "datetime":
                    return "NVARCHAR";
                case "money":
                case "float":
                case "decimal":
                    return "NUMBER";
            }

            return propertyType.ToUpper();
        }

        private static bool IsPrimaryKey(TSModel tsModel, TSProperty property)
        {
            return property.PropertyName.Equals(tsModel.PrimaryKey.PropertyName);
        }
    }
}
