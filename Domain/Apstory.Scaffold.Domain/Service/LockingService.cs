using Apstory.Scaffold.Domain.Util;
using System.Collections.Concurrent;

namespace Apstory.Scaffold.Domain.Service
{
    public class LockingService
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _lockDictionary = new ConcurrentDictionary<string, SemaphoreSlim>();

        public async Task AcquireLockAsync(string lockName)
        {
            Logger.LogDebug($"[Aquire] {lockName}");
            var semaphore = _lockDictionary.GetOrAdd(lockName, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            Logger.LogDebug($"[Locked] {lockName}");
        }

        public void ReleaseLock(string lockName)
        {
            if (_lockDictionary.TryGetValue(lockName, out var semaphore))
                semaphore.Release();
            else
                throw new InvalidOperationException($"No lock found for {lockName}");
        }
    }
}
