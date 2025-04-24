using Apstory.Scaffold.Model.Enum;
using Apstory.Scaffold.Model;
using Apstory.Scaffold.Model.Typescript;
using System.Text;
using Apstory.Scaffold.Domain.Util;

namespace Apstory.Scaffold.Domain.Scaffold
{
    public partial class SqlLiteScaffold
    {
        public SqlLiteScaffold()
        { }

        public async Task<ScaffoldFileResult> GenerateCode(TSModel tsModel, string dalFolder)
        {
            string fileName = $"{tsModel.TSModelName.ToKebabCase()}-dal.service.ts";

            StringBuilder stringBuilder = new StringBuilder();
            GenerateHeader(stringBuilder, tsModel);

            GenerateSqliteCreate(stringBuilder, tsModel);
            GenerateSqliteAddReplace(stringBuilder, tsModel);
            GenerateSqliteGetById(stringBuilder, tsModel);
            GenerateSqliteGetAll(stringBuilder, tsModel);
            GenerateSqliteGetAllUnsynced(stringBuilder, tsModel);
            GenerateSqliteCount(stringBuilder, tsModel);

            if (tsModel.ForeignKeys.Any())
            {
                GenerateSqliteGetByIds(stringBuilder, tsModel);
                GenerateSqliteGetCountByIds(stringBuilder, tsModel);
            }

            GenerateSqliteParseDates(stringBuilder, tsModel);
            stringBuilder.AppendLine("}");

            var filePath = Path.Combine(dalFolder, fileName);
            FileUtils.WriteTextAndDirectory(filePath, stringBuilder.ToString());

            Logger.LogSuccess($"[Created SQL DAL] {filePath}");
            return new ScaffoldFileResult(ScaffoldResult.Created, filePath);
        }
    }
}
