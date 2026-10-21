using CustomFumenProviderWebServer.Databases;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomFumenProviderWebServer.Services
{
    /// <summary>
    /// 更新类 API 的补偿作用域：更新前备份 fumen{musicId} 目录与数据库行。
    /// 调用 CommitAsync 后丢弃备份；未提交就 Dispose 时恢复文件和数据库。
    /// </summary>
    public sealed class FumenUpdateScope : IAsyncDisposable
    {
        private readonly IDbContextFactory<FumenDataDB> dbFactory;
        private readonly FumenStorageService storage;
        private readonly ILogger logger;
        private readonly int musicId;
        private readonly string storagePath;
        private readonly string backupPath;
        private bool hadStorage;

        private FumenSet snapshotSet;
        private FumenOwner snapshotOwner;
        private int? snapshotOwnerMusicId;
        private bool committed;
        private bool rolledBack;

        private FumenUpdateScope(
            IDbContextFactory<FumenDataDB> dbFactory,
            FumenStorageService storage,
            ILogger logger,
            int musicId)
        {
            this.dbFactory = dbFactory;
            this.storage = storage;
            this.logger = logger;
            this.musicId = musicId;
            storagePath = storage.GetStoragePath(musicId);
            backupPath = Path.Combine(Path.GetTempPath(), $"fumen-update-{musicId}-{Guid.NewGuid():N}");
        }

        public static async Task<FumenUpdateScope> BeginAsync(
            IDbContextFactory<FumenDataDB> dbFactory,
            FumenStorageService storage,
            ILogger logger,
            int musicId)
        {
            var scope = new FumenUpdateScope(dbFactory, storage, logger, musicId);
            await scope.CaptureAsync();
            return scope;
        }

        private async Task CaptureAsync()
        {
            if (Directory.Exists(storagePath))
            {
                FileUtils.CopyDirectory(storagePath, backupPath);
                hadStorage = true;
            }

            using var db = await dbFactory.CreateDbContextAsync();
            snapshotSet = await db.FumenSets
                .AsNoTracking()
                .Include(x => x.FumenDifficults)
                .FirstOrDefaultAsync(x => x.MusicId == musicId);
            snapshotOwner = await db.FumenOwners
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.MusicId == musicId);

            if (snapshotSet is not null)
            {
                snapshotOwnerMusicId = await db.FumenSets
                    .AsNoTracking()
                    .Where(x => x.MusicId == musicId)
                    .Select(x => EF.Property<int?>(x, "OwnerMusicId"))
                    .FirstOrDefaultAsync();
            }
        }

        public Task CommitAsync()
        {
            if (!committed)
            {
                committed = true;
                DeleteBackup();
            }

            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (committed || rolledBack)
                return;

            rolledBack = true;
            await RollbackAsync();
        }

        private async Task RollbackAsync()
        {
            try
            {
                storage.DeleteStorage(musicId);
                if (hadStorage)
                    FileUtils.CopyDirectory(backupPath, storagePath);
            }
            catch (Exception e)
            {
                logger.LogError(e, "restore fumen storage failed, musicId:{MusicId}", musicId);
            }
            finally
            {
                DeleteBackup();
            }

            try
            {
                await RestoreDatabaseAsync();
            }
            catch (Exception e)
            {
                logger.LogError(e, "restore fumen database rows failed, musicId:{MusicId}", musicId);
            }
        }

        private async Task RestoreDatabaseAsync()
        {
            using var db = await dbFactory.CreateDbContextAsync();
            using var trans = await db.Database.BeginTransactionAsync();

            try
            {
                var currentDifficults = await db.FumenDifficults.Where(x => x.MusicId == musicId).ToListAsync();
                var currentSets = await db.FumenSets.Where(x => x.MusicId == musicId).ToListAsync();
                var currentOwners = await db.FumenOwners.Where(x => x.MusicId == musicId).ToListAsync();

                db.RemoveRange(currentDifficults);
                db.RemoveRange(currentSets);
                db.RemoveRange(currentOwners);
                await db.SaveChangesAsync();

                if (snapshotOwner is not null)
                {
                    db.FumenOwners.Add(snapshotOwner);
                    await db.SaveChangesAsync();
                }

                if (snapshotSet is not null)
                {
                    db.FumenSets.Add(snapshotSet);
                    db.Entry(snapshotSet).Property("OwnerMusicId").CurrentValue = snapshotOwnerMusicId;
                    await db.SaveChangesAsync();
                }

                await trans.CommitAsync();
            }
            catch
            {
                await trans.RollbackAsync();
                throw;
            }
        }

        private void DeleteBackup()
        {
            try
            {
                if (Directory.Exists(backupPath))
                    Directory.Delete(backupPath, true);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "delete fumen update backup failed: {BackupPath}", backupPath);
            }
        }
    }
}
