using Apstory.Scaffold.Domain.Parser;
using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Sql;
using Microsoft.Extensions.Caching.Memory;

namespace Apstory.Scaffold.Domain.Service
{
    public class SqlTableCachingService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);  // Lock for thread safety

        public SqlTableCachingService(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public SqlTable GetCachedTable(string tablePath)
        {
            var cachedData = _memoryCache.Get<SqlTable>(tablePath);
            if (cachedData is not null)
            {
                Logger.LogDebug($"Cache hit [{Path.GetFileName(tablePath)}]");
                return cachedData;
            }

            Logger.LogDebug($"Cache miss [{Path.GetFileName(tablePath)}]");
            return GetLatestTableAndCache(tablePath);
        }

        public SqlTable GetLatestTableAndCache(string tablePath)
        {
            // Using lock to ensure only one thread populates the cache at a time
            _cacheLock.Wait();
            try
            {
                var tableStr = FileUtils.SafeReadAllText(tablePath);
                Logger.LogDebug($"Read [{Path.GetFileName(tablePath)}]");

                var sqlTable = SqlTableParser.Parse(tableStr);
                Logger.LogDebug($"Parsed [{Path.GetFileName(tablePath)}]");

                var expirationTime = DateTimeOffset.Now.AddDays(1);
                _memoryCache.Set(tablePath, sqlTable, expirationTime);

                return sqlTable;
            }
            catch(Exception ex)
            {
                Logger.LogError($"Error parsing '{tablePath}'\r\n{ex.Message}");
                throw;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public void RemoveCached(string tablePath)
        {
            _memoryCache.Remove(tablePath);
        }
    }
}
