using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Utils;
using Microsoft.EntityFrameworkCore;

namespace CustomFumenProviderWebServer.Tests;

public class FumenRemovalServiceTests
{
    [Fact]
    public async Task RemoveAsync_DeletesDatabaseRowsAndStorageDirectory()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();

        await using (var db = testDb.Factory.CreateDbContext())
        {
            var set = new FumenSet
            {
                MusicId = 20001,
                Title = "Title",
                Artist = "Artist",
                UpdateTime = DateTime.Now,
                PublishState = PublishState.Pending,
            };
            set.FumenDifficults.Add(new FumenDifficult
            {
                MusicId = 20001,
                DifficultIndex = 0,
                Level = 10,
            });
            set.Owner = new FumenOwner
            {
                MusicId = 20001,
                Contact = "contact",
                PasswordHash = "hash",
            };

            db.FumenSets.Add(set);
            await db.SaveChangesAsync();
        }

        var storage = new FumenStorageService(temp.Path);
        var storagePath = storage.GetStoragePath(20001);
        Directory.CreateDirectory(Path.Combine(storagePath, "opt", "music"));
        await File.WriteAllTextAsync(Path.Combine(storagePath, "opt", "music", "Music.xml"), "<MusicData/>");
        await File.WriteAllTextAsync(storage.GetZipPath(20001), "zip");

        var service = new FumenRemovalService(testDb.Factory, storage);
        var result = await service.RemoveAsync(20001);

        Assert.True(result.IsSuccess);
        Assert.False(Directory.Exists(storagePath));

        await using var verifyDb = testDb.Factory.CreateDbContext();
        Assert.Equal(0, await verifyDb.FumenSets.CountAsync());
        Assert.Equal(0, await verifyDb.FumenDifficults.CountAsync());
        Assert.Equal(0, await verifyDb.FumenOwners.CountAsync());
    }

    [Fact]
    public async Task RemoveAsync_UnknownMusicId_ReturnsFalseWithoutThrowing()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var service = new FumenRemovalService(testDb.Factory, new FumenStorageService(temp.Path));

        var result = await service.RemoveAsync(99999);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task RemoveAsync_StorageDoesNotExist_StillDeletesDatabaseRows()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();

        await using (var db = testDb.Factory.CreateDbContext())
        {
            db.FumenSets.Add(new FumenSet
            {
                MusicId = 20002,
                Title = "Title",
                Artist = "Artist",
                UpdateTime = DateTime.Now,
            });
            await db.SaveChangesAsync();
        }

        var service = new FumenRemovalService(testDb.Factory, new FumenStorageService(temp.Path));
        var result = await service.RemoveAsync(20002);

        Assert.True(result.IsSuccess);

        await using var verifyDb = testDb.Factory.CreateDbContext();
        Assert.Equal(0, await verifyDb.FumenSets.CountAsync());
    }
}
