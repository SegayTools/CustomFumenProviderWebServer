using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomFumenProviderWebServer.Tests;

public class FumenUpdateScopeTests
{
    private const int MusicId = 20001;
    private static readonly DateTime InitialUpdateTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static async Task SeedAsync(TestDatabase testDb, FumenStorageService storage, bool createStorage = true)
    {
        await using (var db = testDb.Factory.CreateDbContext())
        {
            var set = new FumenSet
            {
                MusicId = MusicId,
                Title = "old",
                Artist = "artist",
                PublishState = PublishState.Pending,
                UpdateTime = InitialUpdateTime,
            };
            set.FumenDifficults.Add(new FumenDifficult { MusicId = MusicId, DifficultIndex = 0, Level = 10 });
            set.Owner = new FumenOwner { MusicId = MusicId, Contact = "contact", PasswordHash = "hash" };

            db.FumenSets.Add(set);
            await db.SaveChangesAsync();
        }

        if (createStorage)
        {
            var musicFolder = Path.Combine(storage.GetOptPath(MusicId), "music", $"music{MusicId:D4}");
            Directory.CreateDirectory(musicFolder);
            await File.WriteAllTextAsync(Path.Combine(musicFolder, "Music.xml"), "old");
        }
    }

    private static async Task MutateAsync(TestDatabase testDb, FumenStorageService storage)
    {
        await using (var db = testDb.Factory.CreateDbContext())
        {
            var set = await db.FumenSets
                .Include(x => x.FumenDifficults)
                .SingleAsync(x => x.MusicId == MusicId);
            set.Title = "new";
            set.PublishState = PublishState.Published;
            set.FumenDifficults.Add(new FumenDifficult { MusicId = MusicId, DifficultIndex = 1, Level = 15 });
            await db.SaveChangesAsync();
        }

        var musicXml = Path.Combine(storage.GetOptPath(MusicId), "music", $"music{MusicId:D4}", "Music.xml");
        await File.WriteAllTextAsync(musicXml, "new");
        await File.WriteAllTextAsync(Path.Combine(storage.GetOptPath(MusicId), "extra.txt"), "extra");
    }

    [Fact]
    public async Task DisposeWithoutCommit_RestoresStorageAndDatabase()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        await SeedAsync(testDb, storage);

        var scope = await FumenUpdateScope.BeginAsync(testDb.Factory, storage, NullLogger.Instance, MusicId);
        await MutateAsync(testDb, storage);

        await scope.DisposeAsync();

        await using var verifyDb = testDb.Factory.CreateDbContext();
        var restored = await verifyDb.FumenSets
            .Include(x => x.FumenDifficults)
            .SingleAsync(x => x.MusicId == MusicId);

        Assert.Equal("old", restored.Title);
        Assert.Equal(PublishState.Pending, restored.PublishState);
        Assert.Equal(InitialUpdateTime, restored.UpdateTime);
        Assert.Single(restored.FumenDifficults);
        Assert.Equal(10, restored.FumenDifficults.Single().Level);
        Assert.Equal(1, await verifyDb.FumenOwners.CountAsync());

        var ownerMusicId = await verifyDb.FumenSets
            .Where(x => x.MusicId == MusicId)
            .Select(x => EF.Property<int?>(x, "OwnerMusicId"))
            .SingleAsync();
        Assert.Equal(MusicId, ownerMusicId);

        var musicXml = Path.Combine(storage.GetOptPath(MusicId), "music", $"music{MusicId:D4}", "Music.xml");
        Assert.Equal("old", await File.ReadAllTextAsync(musicXml));
        Assert.False(File.Exists(Path.Combine(storage.GetOptPath(MusicId), "extra.txt")));
    }

    [Fact]
    public async Task Commit_KeepsMutatedFilesAndDatabase()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        await SeedAsync(testDb, storage);

        var scope = await FumenUpdateScope.BeginAsync(testDb.Factory, storage, NullLogger.Instance, MusicId);
        await MutateAsync(testDb, storage);
        await scope.CommitAsync();
        await scope.DisposeAsync();

        await using var verifyDb = testDb.Factory.CreateDbContext();
        var updated = await verifyDb.FumenSets
            .Include(x => x.FumenDifficults)
            .SingleAsync(x => x.MusicId == MusicId);

        Assert.Equal("new", updated.Title);
        Assert.Equal(PublishState.Published, updated.PublishState);
        Assert.Equal(2, updated.FumenDifficults.Count);

        var musicXml = Path.Combine(storage.GetOptPath(MusicId), "music", $"music{MusicId:D4}", "Music.xml");
        Assert.Equal("new", await File.ReadAllTextAsync(musicXml));
        Assert.True(File.Exists(Path.Combine(storage.GetOptPath(MusicId), "extra.txt")));
    }

    [Fact]
    public async Task DisposeWithoutCommit_WhenStorageDidNotExist_RemovesCreatedStorage()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        await SeedAsync(testDb, storage, createStorage: false);

        var scope = await FumenUpdateScope.BeginAsync(testDb.Factory, storage, NullLogger.Instance, MusicId);

        var storagePath = storage.GetStoragePath(MusicId);
        Directory.CreateDirectory(storagePath);
        await File.WriteAllTextAsync(Path.Combine(storagePath, "new.txt"), "new");

        await scope.DisposeAsync();

        Assert.False(Directory.Exists(storagePath));

        await using var verifyDb = testDb.Factory.CreateDbContext();
        var restored = await verifyDb.FumenSets.SingleAsync(x => x.MusicId == MusicId);
        Assert.Equal("old", restored.Title);
    }
}
