using System.Collections.Concurrent;

namespace CustomFumenProviderWebServer.Services
{
    /// <summary>
    /// 按 musicId 串行化对同一首谱面的文件/数据库变更。
    /// 并发写同一首曲子会导致:写谱面文件与重打包 zip 互相踩(可能把半截文件打进 zip)、
    /// UpdateSetInfo 的"删除+插入"同主键冲突。不同 musicId 之间不受影响。
    /// </summary>
    public class FumenOperationLock
    {
        private readonly ConcurrentDictionary<int, SemaphoreSlim> semaphores = new();

        public async ValueTask<IDisposable> AcquireAsync(int musicId, CancellationToken cancellationToken = default)
        {
            var semaphore = semaphores.GetOrAdd(musicId, static _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);
            return new Releaser(semaphore);
        }

        private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
        {
            private int disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0)
                    semaphore.Release();
            }
        }
    }
}
