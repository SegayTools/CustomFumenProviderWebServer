using CustomFumenProviderWebServer.Databases;
using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Models.Tables;
using Microsoft.EntityFrameworkCore;

namespace CustomFumenProviderWebServer.Services
{
    /// <summary>
    /// 更新谱面发布状态 / 更新时间，并同步磁盘上的 info.json。
    /// </summary>
    public class FumenStateService
    {
        private readonly IDbContextFactory<FumenDataDB> fumenDataDBFactory;
        private readonly FumenStorageService storageService;

        public FumenStateService(IDbContextFactory<FumenDataDB> fumenDataDBFactory, FumenStorageService storageService)
        {
            this.fumenDataDBFactory = fumenDataDBFactory;
            this.storageService = storageService;
        }

        /// <summary>
        /// 更新状态和时间并重写 info.json。set 不存在或写文件失败时返回失败。
        /// </summary>
        public async Task<Result> UpdateStateAsync(int musicId, PublishState? changeState = default)
        {
            using var db = await fumenDataDBFactory.CreateDbContextAsync();
            using var trans = await db.Database.BeginTransactionAsync();

            try
            {
                var set = await db.FumenSets
                    .Include(x => x.FumenDifficults)
                    .FirstOrDefaultAsync(x => x.MusicId == musicId);

                if (set is null)
                    return new(false, $"FumenSet not found: {musicId}");

                set.UpdateTime = DateTime.Now;
                if (changeState is { } state)
                    set.PublishState = state;

                await db.SaveChangesAsync();

                var writeResult = await storageService.WriteInfoJsonAsync(set);
                if (!writeResult.IsSuccess)
                {
                    await trans.RollbackAsync();
                    return writeResult;
                }

                await trans.CommitAsync();
                return new(true);
            }
            catch (Exception e)
            {
                await trans.RollbackAsync();
                return new(false, $"update set and info.json throw exception: {e.Message}");
            }
        }

        public Task<Result> PublishAsync(int musicId)
        {
            return UpdateStateAsync(musicId, PublishState.Published);
        }

        public async Task<Result> ReadyForPendingAsync(int musicId)
        {
            using var db = await fumenDataDBFactory.CreateDbContextAsync();

            if ((await db.FumenSets.FindAsync(musicId)) is not FumenSet set)
                return new(false, $"FumenSet not found: {musicId}");

            if (set.PublishState == PublishState.Published)
                return new(false, "publishState is Published, cannot set to Pending");

            return await UpdateStateAsync(musicId, PublishState.Pending);
        }
    }
}
