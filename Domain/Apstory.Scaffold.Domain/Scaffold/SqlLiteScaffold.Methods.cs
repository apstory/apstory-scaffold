﻿using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Typescript;
using System.Text;

namespace Apstory.Scaffold.Domain.Scaffold
{
    public partial class SqlLiteScaffold
    {
        private void GenerateSqliteCreate(StringBuilder sb, TSModel tsModel)
        {
            try
            {

                sb.AppendLine($"  private async create{tsModel.TSModelName}() {{");
                sb.AppendLine($"    try {{");

                StringBuilder sbParams = new StringBuilder();
                foreach (var parm in tsModel.Properties)
                    sbParams.Append($"{(sbParams.Length > 0 ? ", " : "")}{parm.PropertyName.ToCamelCase()} {GetSqliteType(parm.PropertyType)}{(IsPrimaryKey(tsModel, parm) ? " tsModel.PrimaryKey KEY" : "")}");

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
                    if (parm.PropertyType == "bit" || parm.PropertyType == "bool")
                        sbParams.Append($"{(sbParams.Length > 0 ? ", " : "")}Number(entity.{parm.PropertyName.ToCamelCase()})");
                    else if (parm.PropertyType == "DateTime")
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

                StringBuilder sbParams = new StringBuilder();
                foreach (var parm in tsModel.Properties)
                    sbParams.Append($"{(sbParams.Length > 0 ? ", " : "")}{parm.PropertyName.ToCamelCase()}");

                sb.AppendLine($"      await this.OpenDB();");
                sb.AppendLine($"      let result = await this.db.query('SELECT {sbParams} FROM {tsModel.TSModelName} WHERE {tsModel.PrimaryKey.PropertyName} = ?', [{tsModel.PrimaryKey.PropertyName.ToCamelCase()}]);");

                if (tsModel.Properties.Any(s => s.PropertyType == "datetime"))
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

                StringBuilder sbParams = new StringBuilder();
                foreach (var parm in tsModel.Properties)
                    sbParams.Append($"{(sbParams.Length > 0 ? ", " : "")}{parm.PropertyName.ToCamelCase()}");

                sb.AppendLine($"      await this.OpenDB();");
                sb.AppendLine($"      let result = await this.db.query('SELECT {sbParams.ToString()} FROM {tsModel.TSModelName}');");

                if (tsModel.Properties.Any(s => s.PropertyType == "datetime"))
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
                sb.AppendLine();
                sb.AppendLine($"  protected ParseDates({tsModel.TSModelName.ToCamelCase()}: Array<{tsModel.TSModelName}>) {{");
                sb.AppendLine($"    {tsModel.TSModelName.ToCamelCase()}.forEach(i => {{");

                StringBuilder sbParams = new StringBuilder();
                foreach (var parm in tsModel.Properties.Where(s => s.PropertyType == "datetime"))
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
                    sb.AppendLine($"  public async Get{tsModel.TSModelName}sBy{propertyName}IsSynced({propertyName.ToCamelCase()}: {propertyType}, isSynced: boolean | undefined) {{");
                    sb.AppendLine($"    try {{");

                    StringBuilder sbParams = new StringBuilder();
                    foreach (var parm in tsModel.Properties)
                        sbParams.Append($"{(sbParams.Length > 0 ? ", " : "")}{parm.PropertyName.ToCamelCase()}");

                    sb.AppendLine($"      await this.OpenDB();");
                    sb.AppendLine($"      let result: DBSQLiteValues;");
                    sb.AppendLine($"      if (typeof isSynced === 'undefined') {{");
                    sb.AppendLine($"        result = await this.db.query('SELECT {sbParams.ToString()} FROM {tsModel.TSModelName} WHERE {propertyName} = ?', [{propertyName.ToCamelCase()}]);");
                    sb.AppendLine($"      }} else {{");
                    sb.AppendLine($"        result = await this.db.query('SELECT {sbParams.ToString()} FROM {tsModel.TSModelName} WHERE {propertyName} = ? AND isSynced = ?', [{propertyName.ToCamelCase()}, Number(isSynced)]);");
                    sb.AppendLine($"      }};");

                    if (tsModel.Properties.Any(s => s.PropertyType == "datetime"))
                        sb.Append($"      this.ParseDates(result.values as Array<{tsModel.TSModelName}>);");

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


        private static string GetSqliteType(string PropertyType)
        {
            switch (PropertyType)
            {
                case "number":
                    return "INTEGER";
                case "string":
                    return "NVARCHAR";
                case "bit":
                    return "INTEGER DEFAULT 1";
                case "date":
                case "datetime":
                    return "NVARCHAR";
                case "money":
                case "float":
                case "decimal":
                    return "NUMBER";
            }

            return PropertyType.ToUpper();
        }

        private static bool IsPrimaryKey(TSModel tsModel, TSProperty property)
        {
            return property.PropertyName.Equals(tsModel.PrimaryKey.PropertyName);
        }
    }
}
