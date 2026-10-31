using System.IO.Compression;
using System.Xml.Linq;
using System.Xml.XPath;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomFumenProviderWebServer.Tests;

public class FumenOptConsistencyServiceTests
{
    private const int MusicId = 20001;

    [Fact]
    public async Task EnsureAll_Enabled_RepairsUnprocessedOpt()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);
        await WriteUnprocessedOptAsync(storage, musicXmlService, set);
        CreateZip(storage, MusicId);

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);

        var master = await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 3));
        Assert.Contains("CREATOR\t[自制谱 Lv.13+] Author3", master);

        for (var i = 0; i < 4; i++)
        {
            var level = set.FumenDifficults.First(d => d.DifficultIndex == i).Level;
            Assert.Contains($"[自制谱 Lv.{FumenChartService.FormatLevel(level)}] Author{i}",
                await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, i)));
        }

        AssertAllZeroLevels(XDocument.Load(Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml")));

        using var zip = ZipFile.OpenRead(storage.GetZipPath(MusicId));
        Assert.Contains("[自制谱 Lv.13+] Author3",
            ReadZipEntry(zip, $"music/music{MusicId:D4}/{MusicId:D4}_03.ogkr"));
        AssertAllZeroLevels(XDocument.Parse(
            ReadZipEntry(zip, $"music/music{MusicId:D4}/Music.xml")));

        var info = await storage.ReadInfoJsonAsync(MusicId);
        Assert.True(info.IsSuccess, info.Message);
        Assert.Equal("Consistency Song", info.Data.Title);
        Assert.Equal(13.7f, info.Data.FumenDifficults.First(d => d.DifficultIndex == 3).Level);
    }

    [Fact]
    public async Task EnsureAll_Enabled_AlreadyProcessed_DoesNotRewrite()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);
        await WriteUnprocessedOptAsync(storage, musicXmlService, set);
        CreateZip(storage, MusicId);

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var first = await service.EnsureAllAsync();
        Assert.True(first.IsSuccess, first.Message);

        var oldTime = DateTime.UtcNow.AddMinutes(-10);
        foreach (var file in EnumerateOptAndZipFiles(storage))
            File.SetLastWriteTimeUtc(file, oldTime);

        var optSnapshot = Snapshot(storage.GetOptPath(MusicId));
        var zipBytes = await File.ReadAllBytesAsync(storage.GetZipPath(MusicId));

        var second = await service.EnsureAllAsync();
        Assert.True(second.IsSuccess, second.Message);

        foreach (var file in EnumerateOptAndZipFiles(storage))
            Assert.Equal(oldTime, File.GetLastWriteTimeUtc(file));

        var optAfter = Snapshot(storage.GetOptPath(MusicId));
        Assert.Equal(optSnapshot.Count, optAfter.Count);
        foreach (var (path, bytes) in optSnapshot)
        {
            Assert.True(optAfter.TryGetValue(path, out var after), path);
            Assert.Equal(bytes, after);
        }

        Assert.Equal(zipBytes, await File.ReadAllBytesAsync(storage.GetZipPath(MusicId)));
    }

    [Fact]
    public async Task EnsureAll_Disabled_RestoresOptState()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);
        await WriteProcessedOptAsync(storage, musicXmlService, set);

        var archiveBytes = System.Text.Encoding.UTF8.GetBytes(
            "VERSION\t1\nCREATOR\tArchiveAuthor\nBPM_DEF\t180\n");
        await WriteFileAsync(storage.GetArchivePath(MusicId, 3), archiveBytes);
        CreateZip(storage, MusicId);

        var service = CreateService(storage, testDb, enableCreatorRewrite: false);
        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);

        Assert.Equal(archiveBytes, await File.ReadAllBytesAsync(storage.GetOptFumenPath(MusicId, 3)));

        var expert = await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 2));
        Assert.Contains("CREATOR\tAuthor2", expert);
        Assert.DoesNotContain("[自制谱", expert);

        var optXml = XDocument.Load(Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml"));
        AssertLevel(optXml, 4, "13", "70");

        using var zip = ZipFile.OpenRead(storage.GetZipPath(MusicId));
        AssertLevel(XDocument.Parse(ReadZipEntry(zip, $"music/music{MusicId:D4}/Music.xml")), 4, "13", "70");
    }

    [Fact]
    public async Task EnsureAll_MissingInfoJson_RebuildsFromDatabase()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await using (var db = testDb.Factory.CreateDbContext())
        {
            db.FumenSets.Add(set);
            await db.SaveChangesAsync();
        }

        await WriteUnprocessedOptAsync(storage, musicXmlService, set);
        Assert.False(File.Exists(storage.GetInfoJsonPath(MusicId)));

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(File.Exists(storage.GetInfoJsonPath(MusicId)));

        var info = await storage.ReadInfoJsonAsync(MusicId);
        Assert.True(info.IsSuccess, info.Message);
        Assert.Equal("Consistency Song", info.Data.Title);
        Assert.Equal(13.7f, info.Data.FumenDifficults.First(d => d.DifficultIndex == 3).Level);

        Assert.Contains("[自制谱 Lv.13+] Author3",
            await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 3)));
        AssertAllZeroLevels(XDocument.Load(Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml")));
    }

    [Fact]
    public async Task EnsureAll_MissingInfoJsonAndDatabase_ReturnsFailureWithoutCreatingInfoJson()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await WriteUnprocessedOptAsync(storage, musicXmlService, set);

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var result = await service.EnsureAllAsync();

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(storage.GetInfoJsonPath(MusicId)));
    }

    [Fact]
    public async Task EnsureAll_Enabled_RepairsWrongPrefix()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);
        await WriteUnprocessedOptAsync(storage, musicXmlService, set);
        CreateZip(storage, MusicId);

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        Assert.True((await service.EnsureAllAsync()).IsSuccess);

        await File.WriteAllTextAsync(
            storage.GetOptFumenPath(MusicId, 3),
            "VERSION\t1\nCREATOR\t[自制谱 Lv.12] WrongAuthor\nBPM_DEF\t180\n");

        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);
        var master = await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 3));
        Assert.Contains("CREATOR\t[自制谱 Lv.13+] Author3", master);
        Assert.DoesNotContain("WrongAuthor", master);
    }

    [Fact]
    public async Task EnsureAll_Enabled_MissingOptChart_RebuildsFromArchive()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);
        await WriteUnprocessedOptAsync(storage, musicXmlService, set);
        CreateZip(storage, MusicId);

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        Assert.True((await service.EnsureAllAsync()).IsSuccess);

        File.Delete(storage.GetOptFumenPath(MusicId, 3));

        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(File.Exists(storage.GetOptFumenPath(MusicId, 3)));
        Assert.Contains("[自制谱 Lv.13+] Author3",
            await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 3)));
    }

    [Fact]
    public async Task EnsureAll_Enabled_MissingOptAndArchive_SkipsWithoutFailure()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);
        await WriteUnprocessedOptAsync(storage, musicXmlService, set);
        CreateZip(storage, MusicId);

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        Assert.True((await service.EnsureAllAsync()).IsSuccess);

        File.Delete(storage.GetOptFumenPath(MusicId, 3));
        File.Delete(storage.GetArchivePath(MusicId, 3));

        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.False(File.Exists(storage.GetOptFumenPath(MusicId, 3)));
    }

    [Fact]
    public async Task EnsureAll_Enabled_RepacksStaleZip_WhenOptAlreadyConsistent()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);
        await WriteUnprocessedOptAsync(storage, musicXmlService, set);
        CreateZip(storage, MusicId);

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var first = await service.EnsureAllAsync();
        Assert.True(first.IsSuccess, first.Message);

        var zipPath = storage.GetZipPath(MusicId);
        var oldTime = DateTime.UtcNow.AddMinutes(-10);
        File.SetLastWriteTimeUtc(zipPath, oldTime);

        var second = await service.EnsureAllAsync();

        Assert.True(second.IsSuccess, second.Message);
        Assert.True(File.GetLastWriteTimeUtc(zipPath) > oldTime);

        using var zip = ZipFile.OpenRead(zipPath);
        AssertAllZeroLevels(XDocument.Parse(
            ReadZipEntry(zip, $"music/music{MusicId:D4}/Music.xml")));
        Assert.Contains("[自制谱 Lv.13+] Author3",
            ReadZipEntry(zip, $"music/music{MusicId:D4}/{MusicId:D4}_03.ogkr"));
    }

    [Fact]
    public async Task EnsureAll_Enabled_WithoutZip_DoesNotCreateZip()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);
        await WriteUnprocessedOptAsync(storage, musicXmlService, set);

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.False(File.Exists(storage.GetZipPath(MusicId)));
        Assert.Contains("[自制谱 Lv.13+] Author3",
            await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 3)));
        AssertAllZeroLevels(XDocument.Load(Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml")));
    }

    [Fact]
    public async Task EnsureAll_Enabled_ExtractsOptFromZip()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);

        var sourceFolder = Path.Combine(temp.Path, "zip-source");
        var sourceMusicFolder = Path.Combine(sourceFolder, "music", $"music{MusicId:D4}");
        Directory.CreateDirectory(sourceMusicFolder);
        for (var i = 0; i < 4; i++)
        {
            var chart = $"VERSION\t1\nCREATOR\tAuthor{i}\nBPM_DEF\t180\n";
            await File.WriteAllTextAsync(Path.Combine(sourceMusicFolder, $"{MusicId:D4}_{i:D2}.ogkr"), chart);
            await WriteFileAsync(storage.GetArchivePath(MusicId, i), chart);
        }

        var xmlResult = await musicXmlService.GenerateMusicXml(set, Path.Combine(sourceFolder, "Music.xml"));
        Assert.True(xmlResult.IsSuccess, xmlResult.Message);
        ZipFile.CreateFromDirectory(sourceFolder, storage.GetZipPath(MusicId));

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(File.Exists(storage.GetOptFumenPath(MusicId, 3)));
        Assert.Contains("[自制谱 Lv.13+] Author3",
            await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 3)));
        AssertAllZeroLevels(XDocument.Load(Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml")));

        using var zip = ZipFile.OpenRead(storage.GetZipPath(MusicId));
        AssertAllZeroLevels(XDocument.Parse(ReadZipEntry(zip, $"music/music{MusicId:D4}/Music.xml")));
        Assert.Contains("[自制谱 Lv.13+] Author3",
            ReadZipEntry(zip, $"music/music{MusicId:D4}/{MusicId:D4}_03.ogkr"));
    }

    [Fact]
    public async Task EnsureAll_Enabled_LunaticRepairsFifthDifficulty()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet(5);

        await storage.WriteInfoJsonAsync(set);
        await WriteUnprocessedOptAsync(storage, musicXmlService, set);

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.Contains("[自制谱 Lv.14] Author4",
            await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 4)));

        var musicXml = XDocument.Load(Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml"));
        AssertAllZeroLevels(musicXml);
        var lunatic = musicXml.XPathSelectElement("/MusicData/FumenData/FumenData[5]");
        Assert.NotNull(lunatic);
        Assert.Equal("20001_04.ogkr", lunatic!.XPathSelectElement("./FumenFile/path")?.Value);
        Assert.Equal("true", musicXml.XPathSelectElement("/MusicData/IsLunatic")?.Value);
    }

    [Fact]
    public async Task EnsureAll_Disabled_SecondRun_DoesNotRewrite()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);
        await WriteProcessedOptAsync(storage, musicXmlService, set);
        await WriteFileAsync(storage.GetArchivePath(MusicId, 3),
            "VERSION\t1\nCREATOR\tArchiveAuthor\nBPM_DEF\t180\n");
        CreateZip(storage, MusicId);

        var service = CreateService(storage, testDb, enableCreatorRewrite: false);
        var first = await service.EnsureAllAsync();
        Assert.True(first.IsSuccess, first.Message);

        var oldTime = DateTime.UtcNow.AddMinutes(-10);
        foreach (var file in EnumerateOptAndZipFiles(storage))
            File.SetLastWriteTimeUtc(file, oldTime);

        var second = await service.EnsureAllAsync();

        Assert.True(second.IsSuccess, second.Message);
        foreach (var file in EnumerateOptAndZipFiles(storage))
            Assert.Equal(oldTime, File.GetLastWriteTimeUtc(file));
    }

    [Fact]
    public async Task EnsureAll_Disabled_ArchiveWithPrefix_StripsOptAndIsIdempotent()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);
        await WriteProcessedOptAsync(storage, musicXmlService, set);
        const string prefixedArchive = "VERSION\t1\nCREATOR\t[自制谱 Lv.13+] Author3\nBPM_DEF\t180\n";
        await WriteFileAsync(storage.GetArchivePath(MusicId, 3), prefixedArchive);
        CreateZip(storage, MusicId);

        var service = CreateService(storage, testDb, enableCreatorRewrite: false);
        var first = await service.EnsureAllAsync();

        Assert.True(first.IsSuccess, first.Message);
        var master = await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 3));
        Assert.Contains("CREATOR\tAuthor3", master);
        Assert.DoesNotContain("[自制谱", master);
        Assert.Equal(prefixedArchive, await File.ReadAllTextAsync(storage.GetArchivePath(MusicId, 3)));

        var oldTime = DateTime.UtcNow.AddMinutes(-10);
        foreach (var file in EnumerateOptAndZipFiles(storage))
            File.SetLastWriteTimeUtc(file, oldTime);

        var second = await service.EnsureAllAsync();

        Assert.True(second.IsSuccess, second.Message);
        foreach (var file in EnumerateOptAndZipFiles(storage))
            Assert.Equal(oldTime, File.GetLastWriteTimeUtc(file));
    }

    [Fact]
    public async Task EnsureAll_Disabled_LevelChange_UpdatesXml()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);
        await WriteUnprocessedOptAsync(storage, musicXmlService, set);
        CreateZip(storage, MusicId);

        var service = CreateService(storage, testDb, enableCreatorRewrite: false);
        Assert.True((await service.EnsureAllAsync()).IsSuccess);

        var info = await storage.ReadInfoJsonAsync(MusicId);
        Assert.True(info.IsSuccess, info.Message);
        info.Data.FumenDifficults.First(d => d.DifficultIndex == 3).Level = 14.75f;
        await storage.WriteInfoJsonAsync(info.Data);

        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);
        var xml = XDocument.Load(Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml"));
        AssertLevel(xml, 4, "14", "75");
    }

    [Fact]
    public async Task EnsureAll_Enabled_LevelChange_RewritesPrefix()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);
        await WriteUnprocessedOptAsync(storage, musicXmlService, set);
        CreateZip(storage, MusicId);

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        Assert.True((await service.EnsureAllAsync()).IsSuccess);

        var info = await storage.ReadInfoJsonAsync(MusicId);
        Assert.True(info.IsSuccess, info.Message);
        info.Data.FumenDifficults.First(d => d.DifficultIndex == 3).Level = 14.75f;
        await storage.WriteInfoJsonAsync(info.Data);

        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.Contains("[自制谱 Lv.14+] Author3",
            await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 3)));
        AssertAllZeroLevels(XDocument.Load(Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml")));

        using var zip = ZipFile.OpenRead(storage.GetZipPath(MusicId));
        Assert.Contains("[自制谱 Lv.14+] Author3",
            ReadZipEntry(zip, $"music/music{MusicId:D4}/{MusicId:D4}_03.ogkr"));
    }

    [Fact]
    public async Task EnsureAll_CorruptInfoJson_RebuildsFromDatabase()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await using (var db = testDb.Factory.CreateDbContext())
        {
            db.FumenSets.Add(set);
            await db.SaveChangesAsync();
        }

        await WriteUnprocessedOptAsync(storage, musicXmlService, set);
        await File.WriteAllTextAsync(storage.GetInfoJsonPath(MusicId), "{not-json");

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);
        var info = await storage.ReadInfoJsonAsync(MusicId);
        Assert.True(info.IsSuccess, info.Message);
        Assert.Equal("Consistency Song", info.Data.Title);
        AssertAllZeroLevels(XDocument.Load(Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml")));
    }

    [Fact]
    public async Task EnsureAll_CorruptOptMusicXml_Rewrites()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);
        await WriteUnprocessedOptAsync(storage, musicXmlService, set);
        var xmlPath = Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml");
        await File.WriteAllTextAsync(xmlPath, "<broken");

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);
        AssertAllZeroLevels(XDocument.Load(xmlPath));
    }

    [Fact]
    public async Task EnsureAll_InvalidXmlPath_Rewrites()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);
        await WriteUnprocessedOptAsync(storage, musicXmlService, set);

        var xmlPath = Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml");
        var musicXml = XDocument.Load(xmlPath);
        musicXml.XPathSelectElement("/MusicData/FumenData/FumenData[1]/FumenFile/path")!.Value = "wrong.ogkr";
        musicXml.Save(xmlPath);

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);
        var rewritten = XDocument.Load(xmlPath);
        Assert.Equal("20001_00.ogkr",
            rewritten.XPathSelectElement("/MusicData/FumenData/FumenData[1]/FumenFile/path")?.Value);
        AssertAllZeroLevels(rewritten);
    }

    [Fact]
    public async Task EnsureAll_NonLunaticMissingDifficulty_FailsAndKeepsXml()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var set = CreateSet(3);

        await storage.WriteInfoJsonAsync(set);
        var musicFolder = storage.GetOptMusicFolder(MusicId);
        Directory.CreateDirectory(musicFolder);
        for (var i = 0; i < 3; i++)
        {
            await WriteFileAsync(
                storage.GetOptFumenPath(MusicId, i),
                $"VERSION\t1\nCREATOR\tAuthor{i}\nBPM_DEF\t180\n");
        }

        var xmlPath = Path.Combine(musicFolder, "Music.xml");
        await File.WriteAllTextAsync(xmlPath, "<old />");

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var result = await service.EnsureAllAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("<old />", await File.ReadAllTextAsync(xmlPath));
    }

    [Fact]
    public async Task EnsureAll_SkipsNonFumenDirectories()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var set = CreateSet();

        await storage.WriteInfoJsonAsync(set);
        await WriteUnprocessedOptAsync(storage, musicXmlService, set);
        CreateZip(storage, MusicId);

        Directory.CreateDirectory(Path.Combine(temp.Path, "fumenabc"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "fumen0"));

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("checked 1, failed 0", result.Message);
    }

    [Fact]
    public async Task EnsureAll_ContinuesAfterOneFailure()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);

        var goodSet = CreateSet();
        await storage.WriteInfoJsonAsync(goodSet);
        await WriteUnprocessedOptAsync(storage, musicXmlService, goodSet);
        CreateZip(storage, MusicId);

        const int badMusicId = 20003;
        Directory.CreateDirectory(storage.GetOptPath(badMusicId));

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var result = await service.EnsureAllAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("checked 2, failed 1", result.Message);
        Assert.Contains("[自制谱 Lv.13+] Author3",
            await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 3)));
        AssertAllZeroLevels(XDocument.Load(Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml")));
    }

    [Fact]
    public async Task EnsureAll_Cancelled_Throws()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        Directory.CreateDirectory(storage.GetStoragePath(MusicId));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.EnsureAllAsync(cts.Token));
    }

    [Fact]
    public async Task EnsureAll_MissingRoot_ReturnsSuccess()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(Path.Combine(temp.Path, "missing"));
        var service = CreateService(storage, testDb, enableCreatorRewrite: true);

        var result = await service.EnsureAllAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("checked 0, failed 0", result.Message);
    }

    [Fact]
    public async Task WriteOptMusicXml_NonLunaticMissingDifficulty_KeepsExistingFile()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var set = CreateSet(3);
        var musicFolder = storage.GetOptMusicFolder(MusicId);
        Directory.CreateDirectory(musicFolder);
        var target = Path.Combine(musicFolder, "Music.xml");
        await File.WriteAllTextAsync(target, "<old />");

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var result = await service.WriteOptMusicXmlAsync(set);

        Assert.False(result.IsSuccess);
        Assert.Contains("必须提供", result.Message);
        Assert.Equal("<old />", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.GetFiles(musicFolder, "*.tmp"));
    }

    [Fact]
    public async Task WriteOptMusicXml_LunaticWritesFifthPath()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        var storage = new FumenStorageService(temp.Path);
        var set = CreateSet(5);

        var service = CreateService(storage, testDb, enableCreatorRewrite: true);
        var result = await service.WriteOptMusicXmlAsync(set);

        Assert.True(result.IsSuccess, result.Message);
        var musicXml = XDocument.Load(Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml"));
        var lunatic = musicXml.XPathSelectElement("/MusicData/FumenData/FumenData[5]");
        Assert.NotNull(lunatic);
        Assert.Equal("0", lunatic!.Element("FumenConstIntegerPart")?.Value);
        Assert.Equal("0", lunatic.Element("FumenConstFractionalPart")?.Value);
        Assert.Equal("20001_04.ogkr", lunatic.XPathSelectElement("./FumenFile/path")?.Value);
        Assert.Equal("true", musicXml.XPathSelectElement("/MusicData/IsLunatic")?.Value);
    }

    private static FumenOptConsistencyService CreateService(
        FumenStorageService storage,
        TestDatabase testDb,
        bool enableCreatorRewrite)
    {
        return new FumenOptConsistencyService(
            storage,
            new FumenChartService(storage, enableCreatorRewrite),
            new MusicXmlService(NullLogger<MusicXmlService>.Instance),
            testDb.Factory,
            new FumenOperationLock(),
            NullLogger<FumenOptConsistencyService>.Instance);
    }

    private static FumenSet CreateSet(int difficultCount = 4, int musicId = MusicId)
    {
        var set = new FumenSet
        {
            MusicId = musicId,
            Title = "Consistency Song",
            Artist = "Artist",
            MusicRightsId = 0,
            MusicRightsName = "-",
            GenreId = 4,
            GenreName = "VARIETY",
            BossCardId = 0,
            BossCardName = "-",
            VersionId = 1,
            VersionName = "Ver.1",
            StageId = 1,
            StageName = "Stage",
            WaveAttribute = "Leaf",
            BossLevel = 1,
            SortOrder = 1,
            BossVoiceNo = 1,
            BossLockHpCoef = 10,
        };

        for (var i = 0; i < difficultCount; i++)
        {
            set.FumenDifficults.Add(new FumenDifficult
            {
                MusicId = musicId,
                DifficultIndex = i,
                Level = 10 + i,
                Creator = $"Author{i}",
            });
        }

        if (difficultCount > 3)
            set.FumenDifficults.First(d => d.DifficultIndex == 3).Level = 13.7f;

        return set;
    }

    private static async Task WriteUnprocessedOptAsync(
        FumenStorageService storage,
        MusicXmlService musicXmlService,
        FumenSet set)
    {
        var musicId = set.MusicId;
        Directory.CreateDirectory(storage.GetOptMusicFolder(musicId));

        foreach (var diff in set.FumenDifficults)
        {
            var i = diff.DifficultIndex;
            await WriteFileAsync(
                storage.GetOptFumenPath(musicId, i),
                $"VERSION\t1\nCREATOR\tAuthor{i}\nBPM_DEF\t180\n");
            await WriteFileAsync(
                storage.GetArchivePath(musicId, i),
                $"VERSION\t1\nCREATOR\tAuthor{i}\nBPM_DEF\t180\n");
        }

        var xmlResult = await musicXmlService.GenerateMusicXml(
            set,
            Path.Combine(storage.GetOptMusicFolder(musicId), "Music.xml"));
        Assert.True(xmlResult.IsSuccess, xmlResult.Message);
    }

    private static async Task WriteProcessedOptAsync(
        FumenStorageService storage,
        MusicXmlService musicXmlService,
        FumenSet set)
    {
        var musicId = set.MusicId;
        Directory.CreateDirectory(storage.GetOptMusicFolder(musicId));

        foreach (var diff in set.FumenDifficults)
        {
            var i = diff.DifficultIndex;
            await WriteFileAsync(
                storage.GetOptFumenPath(musicId, i),
                $"VERSION\t1\nCREATOR\t[自制谱 Lv.{FumenChartService.FormatLevel(diff.Level)}] Author{i}\nBPM_DEF\t180\n");
        }

        var xmlResult = await musicXmlService.GenerateMusicXml(
            set,
            Path.Combine(storage.GetOptMusicFolder(musicId), "Music.xml"),
            zeroLevels: true);
        Assert.True(xmlResult.IsSuccess, xmlResult.Message);
    }

    private static async Task WriteFileAsync(string path, string content)
    {
        await WriteFileAsync(path, System.Text.Encoding.UTF8.GetBytes(content));
    }

    private static async Task WriteFileAsync(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content);
    }

    private static void CreateZip(FumenStorageService storage, int musicId)
    {
        Directory.CreateDirectory(storage.GetOptPath(musicId));
        ZipFile.CreateFromDirectory(storage.GetOptPath(musicId), storage.GetZipPath(musicId),
            CompressionLevel.Optimal, false);
    }

    private static IEnumerable<string> EnumerateOptAndZipFiles(FumenStorageService storage)
    {
        foreach (var file in Directory.EnumerateFiles(storage.GetOptPath(MusicId), "*", SearchOption.AllDirectories))
            yield return file;

        yield return storage.GetZipPath(MusicId);
    }

    private static Dictionary<string, byte[]> Snapshot(string root)
    {
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(root, file), File.ReadAllBytes);
    }

    private static string ReadZipEntry(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName);
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open());
        return reader.ReadToEnd();
    }

    private static void AssertAllZeroLevels(XDocument musicXml)
    {
        for (var i = 1; i <= 5; i++)
            AssertLevel(musicXml, i, "0", "0");
    }

    private static void AssertLevel(XDocument musicXml, int index, string integerPart, string fractionalPart)
    {
        var fumenData = musicXml.XPathSelectElement($"/MusicData/FumenData/FumenData[{index}]");
        Assert.NotNull(fumenData);
        Assert.Equal(integerPart, fumenData!.Element("FumenConstIntegerPart")?.Value);
        Assert.Equal(fractionalPart, fumenData.Element("FumenConstFractionalPart")?.Value);
    }
}
