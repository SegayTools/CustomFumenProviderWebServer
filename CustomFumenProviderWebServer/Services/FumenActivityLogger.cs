#nullable enable

using CustomFumenProviderWebServer.Databases;
using CustomFumenProviderWebServer.Models.Tables;
using Microsoft.EntityFrameworkCore;

namespace CustomFumenProviderWebServer.Services
{
    /// <summary>
    /// 记录谱面活动。审计记录失败不会影响投稿/更新/删除主流程。
    /// </summary>
    public class FumenActivityLogger
    {
        private readonly IDbContextFactory<FumenDataDB> fumenDataDBFactory;
        private readonly ILogger<FumenActivityLogger> logger;

        public FumenActivityLogger(
            IDbContextFactory<FumenDataDB> fumenDataDBFactory,
            ILogger<FumenActivityLogger> logger)
        {
            this.fumenDataDBFactory = fumenDataDBFactory;
            this.logger = logger;
        }

        public async Task RecordAsync(FumenActivity activity, CancellationToken cancellationToken = default)
        {
            try
            {
                if (activity.OccurredAtUtc == default)
                    activity.OccurredAtUtc = DateTime.UtcNow;

                using var db = await fumenDataDBFactory.CreateDbContextAsync(cancellationToken);
                db.FumenActivities.Add(activity);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to record fumen activity: type={ActivityType}, musicId={MusicId}, detail={Detail}.",
                    activity.Type,
                    activity.MusicId,
                    activity.Detail);
            }
        }
    }
}
