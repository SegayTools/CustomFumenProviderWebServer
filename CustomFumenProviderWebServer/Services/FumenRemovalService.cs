using CustomFumenProviderWebServer.Databases;
using CustomFumenProviderWebServer.Models;
using Microsoft.EntityFrameworkCore;

namespace CustomFumenProviderWebServer.Services
{
    /// <summary>
    /// 删除谱面的数据库记录与磁盘存储。
    /// </summary>
    public class FumenRemovalService
    {
        private readonly IDbContextFactory<FumenDataDB> fumenDataDBFactory;
        private readonly FumenStorageService storageService;

        public FumenRemovalService(IDbContextFactory<FumenDataDB> fumenDataDBFactory, FumenStorageService storageService)
        {
            this.fumenDataDBFactory = fumenDataDBFactory;
            this.storageService = storageService;
        }

        public async Task<Result> RemoveAsync(int musicId)
        {
            using var db = await fumenDataDBFactory.CreateDbContextAsync();
            using var trans = await db.Database.BeginTransactionAsync();

            try
            {
                var any = false;
                void delete<T>(List<T> removes)
                {
                    any = any || removes.Count > 0;
                    foreach (var cFumen in removes)
                        db.Remove(cFumen);
                }

                delete(await db.FumenSets.Where(x => x.MusicId == musicId).ToListAsync());
                delete(await db.FumenDifficults.Where(x => x.MusicId == musicId).ToListAsync());
                delete(await db.FumenOwners.Where(x => x.MusicId == musicId).ToListAsync());

                await db.SaveChangesAsync();

                var storageResult = storageService.DeleteStorage(musicId);
                if (!storageResult.IsSuccess)
                {
                    await trans.RollbackAsync();
                    return storageResult;
                }

                await trans.CommitAsync();
                return new(any);
            }
            catch (Exception e)
            {
                await trans.RollbackAsync();
                return new(false, e.Message);
            }
        }
    }
}
