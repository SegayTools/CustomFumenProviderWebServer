using System.IO.Compression;
using System.Text.Json;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Utils;

namespace CustomFumenProviderWebServer.Tests;

public class FumenStorageServiceTests
{
    [Fact]
    public void EnsureOptFolder_ExistingOpt_ReturnsSuccess()
    {
        using var temp = new TempFolderScope();
        var storage = new FumenStorageService(temp.Path);
        Directory.CreateDirectory(storage.GetOptPath(20001));

        var result = storage.EnsureOptFolder(20001);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EnsureOptFolder_ZipExists_ExtractsToOpt()
    {
        using var temp = new TempFolderScope();
        var storage = new FumenStorageService(temp.Path);

        var zipSource = Path.Combine(temp.Path, "zip-source");
        Directory.CreateDirectory(zipSource);
        await File.WriteAllTextAsync(Path.Combine(zipSource, "Music.xml"), "<MusicData/>");

        Directory.CreateDirectory(storage.GetStoragePath(20001));
        ZipFile.CreateFromDirectory(zipSource, storage.GetZipPath(20001));

        var result = storage.EnsureOptFolder(20001);

        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(Path.Combine(storage.GetOptPath(20001), "Music.xml")));
    }

    [Fact]
    public void EnsureOptFolder_NoOptAndNoZip_ReturnsFailureWithMessage()
    {
        using var temp = new TempFolderScope();
        var storage = new FumenStorageService(temp.Path);

        var result = storage.EnsureOptFolder(20001);

        Assert.False(result.IsSuccess);
        Assert.Contains("do not exist", result.Message);
        Assert.False(Directory.Exists(storage.GetOptPath(20001)));
    }

    [Fact]
    public void DeleteStorage_RemovesDirectory_AndIsSafeWhenMissing()
    {
        using var temp = new TempFolderScope();
        var storage = new FumenStorageService(temp.Path);

        var storagePath = storage.GetStoragePath(20001);
        Directory.CreateDirectory(Path.Combine(storagePath, "opt"));
        File.WriteAllText(Path.Combine(storagePath, "info.json"), "{}");

        var first = storage.DeleteStorage(20001);
        var second = storage.DeleteStorage(20001);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.False(Directory.Exists(storagePath));
    }

    [Fact]
    public async Task WriteInfoJsonAsync_OverwritesShorterContentWithoutTrailingGarbage()
    {
        using var temp = new TempFolderScope();
        var storage = new FumenStorageService(temp.Path);

        var set = new FumenSet
        {
            MusicId = 20001,
            Title = new string('x', 1000),
            Artist = "Artist",
        };
        set.FumenDifficults.Add(new FumenDifficult
        {
            MusicId = 20001,
            DifficultIndex = 0,
            Level = 10,
        });

        var first = await storage.WriteInfoJsonAsync(set);
        Assert.True(first.IsSuccess);

        set.Title = "short";
        var second = await storage.WriteInfoJsonAsync(set);
        Assert.True(second.IsSuccess);

        var infoPath = Path.Combine(storage.GetStoragePath(20001), "info.json");
        var json = await File.ReadAllTextAsync(infoPath);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("short", doc.RootElement.GetProperty("Title").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("FumenDifficults").GetArrayLength());
    }
}
