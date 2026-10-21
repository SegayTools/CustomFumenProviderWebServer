using System.Text.Json;
using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Utils;
using Microsoft.EntityFrameworkCore;

namespace CustomFumenProviderWebServer.Tests;

public class FumenStateServiceTests
{
    private const int MusicId = 20001;
    private static readonly DateTime InitialUpdateTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static async Task SeedAsync(TestDatabase testDb, PublishState state = PublishState.Pending)
    {
        await using var db = testDb.Factory.CreateDbContext();
        var set = new FumenSet
        {
            MusicId = MusicId,
            Title = "Title",
            Artist = "Artist",
            PublishState = state,
            UpdateTime = InitialUpdateTime,
        };
        set.FumenDifficults.Add(new FumenDifficult { MusicId = MusicId, DifficultIndex = 0, Level = 10 });
        set.FumenDifficults.Add(new FumenDifficult { MusicId = MusicId, DifficultIndex = 1, Level = 12 });

        db.FumenSets.Add(set);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task PublishAsync_UpdatesDatabaseAndWritesInfoJson()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        await SeedAsync(testDb);

        var storage = new FumenStorageService(temp.Path);
        var service = new FumenStateService(testDb.Factory, storage);

        var result = await service.PublishAsync(MusicId);

        Assert.True(result.IsSuccess);

        await using var db = testDb.Factory.CreateDbContext();
        var set = await db.FumenSets.FindAsync(MusicId);
        Assert.NotNull(set);
        Assert.Equal(PublishState.Published, set.PublishState);

        var infoPath = Path.Combine(storage.GetStoragePath(MusicId), "info.json");
        Assert.True(File.Exists(infoPath));

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(infoPath));
        Assert.Equal(MusicId, doc.RootElement.GetProperty("MusicId").GetInt32());
        Assert.Equal((int)PublishState.Published, doc.RootElement.GetProperty("PublishState").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("FumenDifficults").GetArrayLength());
    }

    [Fact]
    public async Task ReadyForPendingAsync_WritesInfoJson()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        await SeedAsync(testDb, PublishState.NotReadyForPending);

        var storage = new FumenStorageService(temp.Path);
        var service = new FumenStateService(testDb.Factory, storage);

        var result = await service.ReadyForPendingAsync(MusicId);

        Assert.True(result.IsSuccess);

        var infoPath = Path.Combine(storage.GetStoragePath(MusicId), "info.json");
        Assert.True(File.Exists(infoPath));

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(infoPath));
        Assert.Equal((int)PublishState.Pending, doc.RootElement.GetProperty("PublishState").GetInt32());
    }

    [Fact]
    public async Task ReadyForPendingAsync_PublishedChart_IsRejectedWithoutWritingInfoJson()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        await SeedAsync(testDb, PublishState.Published);

        var storage = new FumenStorageService(temp.Path);
        var service = new FumenStateService(testDb.Factory, storage);

        var result = await service.ReadyForPendingAsync(MusicId);

        Assert.False(result.IsSuccess);
        Assert.Contains("Published", result.Message);
        Assert.False(File.Exists(Path.Combine(storage.GetStoragePath(MusicId), "info.json")));
    }

    [Fact]
    public async Task UpdateStateAsync_MissingSet_ReturnsFailure()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var service = new FumenStateService(testDb.Factory, new FumenStorageService(temp.Path));

        var result = await service.UpdateStateAsync(99999, PublishState.Published);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Message);
    }

    [Fact]
    public async Task UpdateStateAsync_InfoJsonWriteFailure_RollsBackDatabaseState()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        await SeedAsync(testDb);

        var storage = new FumenStorageService(temp.Path);

        // 在 storage 目录位置放一个文件,让 WriteInfoJsonAsync 的 Directory.CreateDirectory 失败
        await File.WriteAllTextAsync(storage.GetStoragePath(MusicId), "block");

        var service = new FumenStateService(testDb.Factory, storage);
        var result = await service.PublishAsync(MusicId);

        Assert.False(result.IsSuccess);

        await using var db = testDb.Factory.CreateDbContext();
        var set = await db.FumenSets.FindAsync(MusicId);
        Assert.NotNull(set);
        Assert.Equal(PublishState.Pending, set.PublishState);
        Assert.Equal(InitialUpdateTime, set.UpdateTime);
    }
}
