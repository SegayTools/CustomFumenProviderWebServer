#nullable enable

using CustomFumenProviderWebServer.Databases;
using CustomFumenProviderWebServer.Models.Tables;
using Microsoft.EntityFrameworkCore;

namespace CustomFumenProviderWebServer.Services.Email
{
    /// <summary>
    /// 每 REPORT_INTERVAL_HOURS 小时汇总一次投稿/修改/删除活动并发送邮件。
    /// 上一次成功报表的时间从数据库读取，服务重启后不会丢失统计窗口。
    /// 首次启动且没有历史报表时，会立即发送一份覆盖最近一个周期（默认 6 小时）的报表。
    /// </summary>
    public sealed class ActivityReportHostedService : BackgroundService
    {
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(15);

        private readonly IDbContextFactory<FumenDataDB> fumenDataDBFactory;
        private readonly SmtpEmailSender emailSender;
        private readonly EmailOptions emailOptions;
        private readonly ActivityReportOptions reportOptions;
        private readonly ILogger<ActivityReportHostedService> logger;

        public ActivityReportHostedService(
            IDbContextFactory<FumenDataDB> fumenDataDBFactory,
            SmtpEmailSender emailSender,
            EmailOptions emailOptions,
            ActivityReportOptions reportOptions,
            ILogger<ActivityReportHostedService> logger)
        {
            this.fumenDataDBFactory = fumenDataDBFactory;
            this.emailSender = emailSender;
            this.emailOptions = emailOptions;
            this.reportOptions = reportOptions;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!reportOptions.Enabled)
            {
                logger.LogInformation("Activity report email is disabled by REPORT_ENABLED.");
                return;
            }

            if (!reportOptions.IsConfigured)
            {
                logger.LogWarning(
                    "Activity report email is disabled because REPORT_* settings are invalid. " +
                    "Recipients={Recipients}; IntervalHours={IntervalHours}; MaxRows={MaxRows}.",
                    string.Join(", ", reportOptions.Recipients),
                    reportOptions.IntervalHours,
                    reportOptions.MaxRows);
                return;
            }

            if (!emailOptions.IsConfigured)
            {
                logger.LogWarning("Activity report email is disabled because SMTP is not configured.");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var lastReportEndUtc = await GetLastReportEndUtcAsync(stoppingToken);
                    var now = DateTime.UtcNow;
                    var periodStartUtc = lastReportEndUtc ?? now.AddHours(-reportOptions.IntervalHours);
                    var nextSendUtc = lastReportEndUtc?.Add(reportOptions.Interval) ?? now;

                    if (nextSendUtc > now)
                    {
                        var delay = nextSendUtc - now;
                        logger.LogInformation("Next activity report is scheduled in {Delay}.", delay);
                        await Task.Delay(delay, stoppingToken);
                    }

                    var periodEndUtc = DateTime.UtcNow;
                    if (periodEndUtc <= periodStartUtc)
                        periodEndUtc = periodStartUtc.AddSeconds(1);

                    var activities = await LoadActivitiesAsync(periodStartUtc, periodEndUtc, stoppingToken);

                    if (activities.Count == 0)
                    {
                        // 没有投稿/修改/删除时不发邮件，但仍然记录本周期，避免下次循环立刻再次检查。
                        await SaveReportAsync(
                            periodStartUtc,
                            periodEndUtc,
                            eventCount: 0,
                            recipients: null,
                            stoppingToken);
                        logger.LogInformation(
                            "No fumen activity in period {PeriodStartUtc:O} - {PeriodEndUtc:O}; report email skipped.",
                            periodStartUtc,
                            periodEndUtc);
                        continue;
                    }

                    var email = ActivityReportBuilder.Build(
                        reportOptions,
                        periodStartUtc,
                        periodEndUtc,
                        activities);

                    await emailSender.SendAsync(email, stoppingToken);

                    await SaveReportAsync(
                        periodStartUtc,
                        periodEndUtc,
                        eventCount: activities.Count,
                        recipients: string.Join(", ", reportOptions.Recipients),
                        stoppingToken);

                    logger.LogInformation(
                        "Activity report sent to {Recipients}. Period: {PeriodStartUtc:O} - {PeriodEndUtc:O}; events: {EventCount}.",
                        string.Join(", ", reportOptions.Recipients),
                        periodStartUtc,
                        periodEndUtc,
                        activities.Count);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send the activity report. Retrying in {RetryDelay}.", RetryDelay);
                    try
                    {
                        await Task.Delay(RetryDelay, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task SaveReportAsync(
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            int eventCount,
            string? recipients,
            CancellationToken cancellationToken)
        {
            using var db = await fumenDataDBFactory.CreateDbContextAsync(cancellationToken);
            db.FumenActivityReports.Add(new FumenActivityReport
            {
                PeriodStartUtc = periodStartUtc,
                PeriodEndUtc = periodEndUtc,
                SentAtUtc = DateTime.UtcNow,
                EventCount = eventCount,
                Recipients = recipients,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        private async Task<List<FumenActivity>> LoadActivitiesAsync(
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            CancellationToken cancellationToken)
        {
            using var db = await fumenDataDBFactory.CreateDbContextAsync(cancellationToken);
            return await db.FumenActivities
                .AsNoTracking()
                .Where(x => x.OccurredAtUtc >= periodStartUtc && x.OccurredAtUtc < periodEndUtc)
                .OrderBy(x => x.OccurredAtUtc)
                .ToListAsync(cancellationToken);
        }

        private async Task<DateTime?> GetLastReportEndUtcAsync(CancellationToken cancellationToken)
        {
            using var db = await fumenDataDBFactory.CreateDbContextAsync(cancellationToken);
            var lastReport = await db.FumenActivityReports
                .AsNoTracking()
                .OrderByDescending(x => x.SentAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            return lastReport?.PeriodEndUtc;
        }
    }
}
