using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomFumenProviderWebServer.Tests;

public class FumenActivityLoggerTests
{
    [Fact]
    public async Task RecordAsync_PersistsActivity()
    {
        using var testDb = new TestDatabase();
        var logger = new FumenActivityLogger(testDb.Factory, NullLogger<FumenActivityLogger>.Instance);
        var occurredAt = new DateTime(2026, 11, 3, 1, 2, 3, DateTimeKind.Utc);

        await logger.RecordAsync(new FumenActivity
        {
            MusicId = 20001,
            Title = "Test Song",
            Artist = "Test Artist",
            OwnerContact = "owner@example.com",
            OwnerImContact = "owner-im",
            Type = FumenActivityType.Modified,
            Detail = "jacket",
            OccurredAtUtc = occurredAt,
        });

        using var db = await testDb.Factory.CreateDbContextAsync();
        var activity = Assert.Single(await db.FumenActivities.ToListAsync());

        Assert.Equal(20001, activity.MusicId);
        Assert.Equal("Test Song", activity.Title);
        Assert.Equal(FumenActivityType.Modified, activity.Type);
        Assert.Equal("jacket", activity.Detail);
        Assert.Equal(occurredAt, activity.OccurredAtUtc);
    }

    [Fact]
    public async Task RecordAsync_SetsUtcTimestampWhenMissing()
    {
        using var testDb = new TestDatabase();
        var logger = new FumenActivityLogger(testDb.Factory, NullLogger<FumenActivityLogger>.Instance);

        var before = DateTime.UtcNow;
        await logger.RecordAsync(new FumenActivity
        {
            MusicId = 20002,
            Type = FumenActivityType.Submitted,
        });
        var after = DateTime.UtcNow;

        using var db = await testDb.Factory.CreateDbContextAsync();
        var activity = Assert.Single(await db.FumenActivities.ToListAsync());
        Assert.InRange(activity.OccurredAtUtc, before, after);
    }
}
