using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using CustomFumenProviderWebServer.Controllers;
using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomFumenProviderWebServer.Tests;

[CollectionDefinition("FumenControllerEnv", DisableParallelization = true)]
public class FumenControllerEnvCollection
{
}

[Collection("FumenControllerEnv")]
public class FumenControllerTests
{
    private const int MusicId = 20001;
    private const string Password = "test-password";

    [Fact]
    public async Task UpdateOgkrFumen_ArchivesRawChart_AndWritesProcessedOptCopy()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        using var env = UseFumenDirectory(temp.Path);
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var controller = await PrepareAsync(testDb, storage, musicXmlService);

        var uploadBytes = Encoding.UTF8.GetBytes("VERSION\t1\nCREATOR\tNewAuthor\nBPM_DEF\t180\nTAP\t0\t1\n");
        var formFile = CreateFormFile(uploadBytes);

        var result = await controller.UpdateOgkrFumen(formFile, 13.7f, 3, MusicId, Password);

        Assert.True(result.IsSuccess, result.Message);

        var archiveBytes = await File.ReadAllBytesAsync(storage.GetArchivePath(MusicId, 3));
        Assert.Equal(uploadBytes, archiveBytes);
        Assert.EndsWith("master.ogkr", storage.GetArchivePath(MusicId, 3));

        var optChart = await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 3));
        Assert.Contains("CREATOR\t[自制谱 Lv.13+] NewAuthor", optChart);

        await using var verifyDb = testDb.Factory.CreateDbContext();
        var masterDiff = await verifyDb.FumenDifficults.FindAsync(MusicId, 3);
        Assert.NotNull(masterDiff);
        Assert.Equal(13.7f, masterDiff!.Level);
        Assert.Equal("NewAuthor", masterDiff.Creator);

        using var zip = ZipFile.OpenRead(storage.GetZipPath(MusicId));
        var zipEntry = zip.GetEntry($"music/music{MusicId:D4}/{MusicId:D4}_03.ogkr");
        Assert.NotNull(zipEntry);
        using var reader = new StreamReader(zipEntry!.Open());
        Assert.Contains("[自制谱 Lv.13+] NewAuthor", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task UpdateOgkrFumen_WithoutChangeDiff_UsesExistingLevelForCreatorTag()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        using var env = UseFumenDirectory(temp.Path);
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var controller = await PrepareAsync(testDb, storage, musicXmlService);

        var formFile = CreateFormFile(Encoding.UTF8.GetBytes("VERSION\t1\nCREATOR\tNewAuthor\nBPM_DEF\t180\n"));

        var result = await controller.UpdateOgkrFumen(formFile, null, 3, MusicId, Password);

        Assert.True(result.IsSuccess, result.Message);

        var optChart = await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 3));
        Assert.Contains("CREATOR\t[自制谱 Lv.13] NewAuthor", optChart);

        await using var verifyDb = testDb.Factory.CreateDbContext();
        var masterDiff = await verifyDb.FumenDifficults.FindAsync(MusicId, 3);
        Assert.NotNull(masterDiff);
        Assert.Equal(13f, masterDiff!.Level);
        Assert.Equal("NewAuthor", masterDiff.Creator);
    }

    [Fact]
    public async Task UpdateOgkrFumen_WithoutFile_OnlyChangesDifficulty()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        using var env = UseFumenDirectory(temp.Path);
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var controller = await PrepareAsync(testDb, storage, musicXmlService);

        var result = await controller.UpdateOgkrFumen(null!, 13.7f, 3, MusicId, Password);

        Assert.True(result.IsSuccess, result.Message);

        //没有谱面文件时不生成根目录归档
        Assert.False(File.Exists(storage.GetArchivePath(MusicId, 3)));

        //只改 CREATOR 的 Lv，原谱师保留
        var master = await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 3));
        Assert.Contains("CREATOR\t[自制谱 Lv.13+] Author3", master);

        //其它难度按各自定数重写
        var expert = await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 2));
        Assert.Contains("CREATOR\t[自制谱 Lv.12] Author2", expert);

        await using (var verifyDb = testDb.Factory.CreateDbContext())
        {
            var masterDiff = await verifyDb.FumenDifficults.FindAsync(MusicId, 3);
            Assert.NotNull(masterDiff);
            Assert.Equal(13.7f, masterDiff!.Level);
        }

        var musicXml = XDocument.Load(Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml"));
        var masterData = musicXml.XPathSelectElement("/MusicData/FumenData/FumenData[4]");
        Assert.NotNull(masterData);
        Assert.Equal("13", masterData!.Element("FumenConstIntegerPart")?.Value);
        Assert.Equal("70", masterData.Element("FumenConstFractionalPart")?.Value);

        using var zip = ZipFile.OpenRead(storage.GetZipPath(MusicId));
        var entry = zip.GetEntry($"music/music{MusicId:D4}/{MusicId:D4}_03.ogkr");
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open());
        Assert.Contains("[自制谱 Lv.13+] Author3", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task UpdateOgkrFumen_WithoutFileOrChangeDiff_Returns400()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        using var env = UseFumenDirectory(temp.Path);
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var controller = await PrepareAsync(testDb, storage, musicXmlService);

        var result = await controller.UpdateOgkrFumen(null!, null, 3, MusicId, Password);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, controller.HttpContext.Response.StatusCode);
        Assert.False(File.Exists(storage.GetArchivePath(MusicId, 3)));
    }

    [Fact]
    public async Task UpdateOgkrFumen_WithoutFile_MissingDifficulty_Returns400()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        using var env = UseFumenDirectory(temp.Path);
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var controller = await PrepareAsync(testDb, storage, musicXmlService);

        var result = await controller.UpdateOgkrFumen(null!, 13.7f, 4, MusicId, Password);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, controller.HttpContext.Response.StatusCode);
        Assert.False(File.Exists(storage.GetArchivePath(MusicId, 4)));
    }

    [Fact]
    public void ChartServiceSwitch_DefaultsToOff_AndReadsEnvironment()
    {
        var variable = FumenChartService.EnableCreatorRewriteEnvironmentVariable;
        var previous = Environment.GetEnvironmentVariable(variable);

        try
        {
            Environment.SetEnvironmentVariable(variable, null);
            Assert.False(new FumenChartService(new FumenStorageService(Path.GetTempPath())).IsCreatorRewriteEnabled);

            Environment.SetEnvironmentVariable(variable, "true");
            Assert.True(new FumenChartService(new FumenStorageService(Path.GetTempPath())).IsCreatorRewriteEnabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    private static FormFile CreateFormFile(byte[] bytes, string fileName = "upload-name.ogkr")
    {
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "formFile", fileName);
    }

    private static async Task<FumenController> PrepareAsync(
        TestDatabase testDb,
        FumenStorageService storage,
        MusicXmlService musicXmlService,
        AudioService? audioService = null,
        JacketService? jacketService = null)
    {
        var seedSet = CreateSeedSet();
        var musicFolder = storage.GetOptMusicFolder(MusicId);
        Directory.CreateDirectory(musicFolder);

        for (int diffIdx = 0; diffIdx < 4; diffIdx++)
        {
            await File.WriteAllTextAsync(
                storage.GetOptFumenPath(MusicId, diffIdx),
                $"VERSION\t1\nCREATOR\tAuthor{diffIdx}\nBPM_DEF\t180\n");
        }

        await musicXmlService.GenerateMusicXml(seedSet, Path.Combine(musicFolder, "Music.xml"));

        await using (var db = testDb.Factory.CreateDbContext())
        {
            db.FumenSets.Add(seedSet);
            await db.SaveChangesAsync();
        }

        return CreateController(testDb, storage, musicXmlService, audioService, jacketService);
    }

    /// <summary>
    /// 完整种子：opt、Music.xml、info.json、归档、zip、DB，全部互相一致，
    /// 用于验证 update 类接口对磁盘与数据库的维护结果。
    /// </summary>
    private static async Task<FumenController> PrepareWithInfoAsync(
        TestDatabase testDb,
        FumenStorageService storage,
        MusicXmlService musicXmlService,
        AudioService? audioService = null,
        JacketService? jacketService = null)
    {
        var controller = await PrepareAsync(testDb, storage, musicXmlService, audioService, jacketService);

        var seedSet = CreateSeedSet();
        var writeInfo = await storage.WriteInfoJsonAsync(seedSet);
        Assert.True(writeInfo.IsSuccess, writeInfo.Message);

        for (int diffIdx = 0; diffIdx < 4; diffIdx++)
        {
            await File.WriteAllTextAsync(
                storage.GetArchivePath(MusicId, diffIdx),
                $"VERSION\t1\nCREATOR\tAuthor{diffIdx}\nBPM_DEF\t180\n");
        }

        ZipFile.CreateFromDirectory(
            storage.GetOptPath(MusicId),
            storage.GetZipPath(MusicId),
            CompressionLevel.Optimal,
            false);

        return controller;
    }

    private static string ReadZipEntry(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName);
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open());
        return reader.ReadToEnd();
    }

    private static async Task<FumenSet> ReadInfoAsync(FumenStorageService storage)
    {
        var info = await storage.ReadInfoJsonAsync(MusicId);
        Assert.True(info.IsSuccess, info.Message);
        return info.Data;
    }

    private static IDisposable UseFumenDirectory(string path)
    {
        var previous = Environment.GetEnvironmentVariable("FumenDirectory");
        Environment.SetEnvironmentVariable("FumenDirectory", path);
        return new EnvironmentRestorer(previous);
    }

    private sealed class EnvironmentRestorer : IDisposable
    {
        private readonly string? previous;

        public EnvironmentRestorer(string? previous)
        {
            this.previous = previous;
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("FumenDirectory", previous);
        }
    }

    private static FumenController CreateController(
        TestDatabase testDb,
        FumenStorageService storage,
        MusicXmlService musicXmlService,
        AudioService? audioService = null,
        JacketService? jacketService = null)
    {
        var controller = new FumenController(
            NullLogger<FumenController>.Instance,
            testDb.Factory,
            audioService!,
            jacketService!,
            musicXmlService,
            new FumenOperationLock(),
            new AdminAuthService(NullLogger<AdminAuthService>.Instance),
            new FumenStateService(testDb.Factory, storage),
            storage,
            new FumenChartService(storage, true),
            null!,
            null!);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        return controller;
    }

    private static FumenSet CreateSeedSet()
    {
        var set = new FumenSet
        {
            MusicId = MusicId,
            Title = "Test Song",
            Artist = "Test Artist",
            GenreId = 4,
            GenreName = "VARIETY",
            MusicRightsId = 0,
            MusicRightsName = "-",
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
            PublishState = PublishState.Pending,
            UpdateTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Owner = new FumenOwner
            {
                MusicId = MusicId,
                Contact = "contact",
                PasswordHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Password))),
            },
        };

        for (int diffIdx = 0; diffIdx < 4; diffIdx++)
        {
            set.FumenDifficults.Add(new FumenDifficult
            {
                MusicId = MusicId,
                DifficultIndex = diffIdx,
                Level = 10 + diffIdx,
            });
        }

        return set;
    }

    [Fact]
    public async Task UpdateFile_DefaultFalse_WritesFileWithoutRepacking()
    {
        await WithAdminPassword(Password, async () =>
        {
            using var temp = new TempFolderScope();
            using var testDb = new TestDatabase();
            using var env = UseFumenDirectory(temp.Path);
            var storage = new FumenStorageService(temp.Path);
            var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
            var controller = await PrepareAsync(testDb, storage, musicXmlService);

            var bytes = Encoding.UTF8.GetBytes("VERSION\t1\nCREATOR\tNewAuthor\nBPM_DEF\t180\n");
            var formFile = CreateFormFile(bytes);

            var result = await controller.UpdateFile(
                formFile, MusicId, $"opt/music/music{MusicId:D4}/{MusicId:D4}_04.ogkr", Password);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(storage.GetOptFumenPath(MusicId, 4)));
            Assert.False(File.Exists(storage.GetZipPath(MusicId)));
            Assert.False(File.Exists(Path.Combine(storage.GetStoragePath(MusicId), "info.json")));
        });
    }

    [Fact]
    public async Task UpdateFile_RepackZipTrue_RebuildsZipAndUpdatesInfo()
    {
        await WithAdminPassword(Password, async () =>
        {
            using var temp = new TempFolderScope();
            using var testDb = new TestDatabase();
            using var env = UseFumenDirectory(temp.Path);
            var storage = new FumenStorageService(temp.Path);
            var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
            var controller = await PrepareAsync(testDb, storage, musicXmlService);
            var before = DateTime.Now.AddSeconds(-2);

            var bytes = Encoding.UTF8.GetBytes("VERSION\t1\nCREATOR\tNewAuthor\nBPM_DEF\t180\nTAP\t0\t1\n");
            var formFile = CreateFormFile(bytes);

            var result = await controller.UpdateFile(
                formFile, MusicId, $"opt/music/music{MusicId:D4}/{MusicId:D4}_04.ogkr", Password, repackZip: true);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(storage.GetOptFumenPath(MusicId, 4)));
            Assert.True(File.Exists(Path.Combine(storage.GetStoragePath(MusicId), "info.json")));

            using var zip = ZipFile.OpenRead(storage.GetZipPath(MusicId));
            var entry = zip.GetEntry($"music/music{MusicId:D4}/{MusicId:D4}_04.ogkr");
            Assert.NotNull(entry);
            using var entryStream = entry!.Open();
            using var entryContent = new MemoryStream();
            await entryStream.CopyToAsync(entryContent);
            Assert.Equal(bytes, entryContent.ToArray());

            await using var verifyDb = testDb.Factory.CreateDbContext();
            var set = await verifyDb.FumenSets.FindAsync(MusicId);
            Assert.NotNull(set);
            Assert.True(set!.UpdateTime >= before);
        });
    }

    [Fact]
    public async Task UpdateFile_WithWrongPassword_Returns403AndDoesNotWrite()
    {
        await WithAdminPassword(Password, async () =>
        {
            using var temp = new TempFolderScope();
            using var testDb = new TestDatabase();
            using var env = UseFumenDirectory(temp.Path);
            var storage = new FumenStorageService(temp.Path);
            var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
            var controller = await PrepareAsync(testDb, storage, musicXmlService);
            var formFile = CreateFormFile(new byte[] { 1 });

            var result = await controller.UpdateFile(
                formFile, MusicId, $"opt/music/music{MusicId:D4}/{MusicId:D4}_04.ogkr", "wrong-password");

            Assert.False(result.IsSuccess);
            Assert.Equal(403, controller.HttpContext.Response.StatusCode);
            Assert.False(File.Exists(storage.GetOptFumenPath(MusicId, 4)));
        });
    }

    [Fact]
    public async Task UpdateFile_WithPathTraversal_Returns400()
    {
        await WithAdminPassword(Password, async () =>
        {
            using var temp = new TempFolderScope();
            using var testDb = new TestDatabase();
            using var env = UseFumenDirectory(temp.Path);
            var storage = new FumenStorageService(temp.Path);
            var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
            var controller = await PrepareAsync(testDb, storage, musicXmlService);

            var fileName = $"evil-{Guid.NewGuid():N}.txt";
            var outsidePath = Path.Combine(temp.Path, fileName);
            var formFile = CreateFormFile(new byte[] { 1 });

            var result = await controller.UpdateFile(formFile, MusicId, $"../{fileName}", Password);

            Assert.False(result.IsSuccess);
            Assert.Equal(400, controller.HttpContext.Response.StatusCode);
            Assert.False(File.Exists(outsidePath));
        });
    }

    [Fact]
    public async Task UpdateInfo_RendersMusicXmlFromInfoJson_NotFromDiskXml()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        using var env = UseFumenDirectory(temp.Path);
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var controller = await PrepareAsync(testDb, storage, musicXmlService);

        var infoSet = CreateSeedSet();
        infoSet.Title = "Info Title";
        var writeInfo = await storage.WriteInfoJsonAsync(infoSet);
        Assert.True(writeInfo.IsSuccess, writeInfo.Message);

        //磁盘 XML 与 info.json 故意不一致：更新后必须以 info.json 为准
        var staleSet = CreateSeedSet();
        staleSet.Title = "Stale XML Title";
        await musicXmlService.GenerateMusicXml(staleSet, Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml"));

        var result = await controller.UpdateSetInfo(MusicId, Password, reGenerateMusicXml: true);

        Assert.True(result.IsSuccess, result.Message);

        var xmlText = await File.ReadAllTextAsync(Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml"));
        Assert.Contains("Info Title", xmlText);
        Assert.DoesNotContain("Stale XML Title", xmlText);

        var info = await storage.ReadInfoJsonAsync(MusicId);
        Assert.True(info.IsSuccess, info.Message);
        Assert.Equal("Info Title", info.Data.Title);

        await using var db = testDb.Factory.CreateDbContext();
        var dbSet = await db.FumenSets.FindAsync(MusicId);
        Assert.NotNull(dbSet);
        Assert.Equal("Info Title", dbSet!.Title);
        Assert.Equal(PublishState.Pending, dbSet.PublishState);
    }

    [Fact]
    public async Task UpdateMusicXml_PersistsUploadedXmlToInfoJson_ThenRendersOptXml()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        using var env = UseFumenDirectory(temp.Path);
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var controller = await PrepareAsync(testDb, storage, musicXmlService);

        var uploadedSet = CreateSeedSet();
        uploadedSet.Title = "Uploaded Title";
        var uploadPath = Path.Combine(temp.Path, "upload.xml");
        await musicXmlService.GenerateMusicXml(uploadedSet, uploadPath);
        var formFile = CreateFormFile(await File.ReadAllBytesAsync(uploadPath), "Music.xml");

        var result = await controller.UpdateMusicXml(formFile, MusicId, Password);

        Assert.True(result.IsSuccess, result.Message);

        var info = await storage.ReadInfoJsonAsync(MusicId);
        Assert.True(info.IsSuccess, info.Message);
        Assert.Equal("Uploaded Title", info.Data.Title);
        Assert.Equal(PublishState.NotReadyForPending, info.Data.PublishState);

        Assert.Contains("Uploaded Title", await File.ReadAllTextAsync(
            Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml")));

        using var zip = ZipFile.OpenRead(storage.GetZipPath(MusicId));
        var xmlEntry = zip.GetEntry($"music/music{MusicId:D4}/Music.xml");
        Assert.NotNull(xmlEntry);
        using var reader = new StreamReader(xmlEntry!.Open());
        Assert.Contains("Uploaded Title", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task UpdateInfo_WithoutInfoJson_ImportsLegacyMusicXmlOnce()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        using var env = UseFumenDirectory(temp.Path);
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var controller = await PrepareAsync(testDb, storage, musicXmlService);

        var legacySet = CreateSeedSet();
        legacySet.Title = "Legacy Title";
        await musicXmlService.GenerateMusicXml(legacySet, Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml"));

        var result = await controller.UpdateSetInfo(MusicId, Password);

        Assert.True(result.IsSuccess, result.Message);

        var info = await storage.ReadInfoJsonAsync(MusicId);
        Assert.True(info.IsSuccess, info.Message);
        Assert.Equal("Legacy Title", info.Data.Title);

        await using var db = testDb.Factory.CreateDbContext();
        var dbSet = await db.FumenSets.FindAsync(MusicId);
        Assert.NotNull(dbSet);
        Assert.Equal("Legacy Title", dbSet!.Title);
    }

    [Fact]
    public async Task UpdateXml_UpdatesInfoDbXmlZipAndCreator()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        using var env = UseFumenDirectory(temp.Path);
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var controller = await PrepareWithInfoAsync(testDb, storage, musicXmlService);
        var before = DateTime.Now.AddSeconds(-2);

        var uploadedSet = CreateSeedSet();
        uploadedSet.Title = "XML Updated Title";
        uploadedSet.FumenDifficults.First(d => d.DifficultIndex == 3).Level = 14.75f;
        var uploadPath = Path.Combine(temp.Path, "upload.xml");
        await musicXmlService.GenerateMusicXml(uploadedSet, uploadPath);

        var result = await controller.UpdateMusicXml(
            CreateFormFile(await File.ReadAllBytesAsync(uploadPath), "Music.xml"), MusicId, Password);

        Assert.True(result.IsSuccess, result.Message);

        var info = await ReadInfoAsync(storage);
        Assert.Equal("XML Updated Title", info.Title);
        Assert.Equal(14.75f, info.FumenDifficults.First(d => d.DifficultIndex == 3).Level);
        Assert.Equal(PublishState.NotReadyForPending, info.PublishState);
        Assert.True(info.UpdateTime >= before);

        var xmlText = await File.ReadAllTextAsync(Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml"));
        Assert.Contains("XML Updated Title", xmlText);
        Assert.Contains("<FumenConstIntegerPart>14</FumenConstIntegerPart>", xmlText);
        Assert.Contains("<FumenConstFractionalPart>75</FumenConstFractionalPart>", xmlText);

        Assert.Contains("[自制谱 Lv.14+] Author3",
            await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 3)));
        Assert.Equal("VERSION\t1\nCREATOR\tAuthor3\nBPM_DEF\t180\n",
            await File.ReadAllTextAsync(storage.GetArchivePath(MusicId, 3)));

        using var zip = ZipFile.OpenRead(storage.GetZipPath(MusicId));
        Assert.Contains("XML Updated Title", ReadZipEntry(zip, $"music/music{MusicId:D4}/Music.xml"));
        Assert.Contains("[自制谱 Lv.14+]", ReadZipEntry(zip, $"music/music{MusicId:D4}/{MusicId:D4}_03.ogkr"));

        await using var db = testDb.Factory.CreateDbContext();
        var dbSet = await db.FumenSets.Include(x => x.FumenDifficults).FirstAsync(x => x.MusicId == MusicId);
        Assert.Equal("XML Updated Title", dbSet.Title);
        Assert.Equal(14.75f, dbSet.FumenDifficults.First(d => d.DifficultIndex == 3).Level);
        Assert.Equal(PublishState.NotReadyForPending, dbSet.PublishState);
    }

    [Fact]
    public async Task UpdateOgkr_UpdatesArchiveOptInfoDbXmlAndZip()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        using var env = UseFumenDirectory(temp.Path);
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var controller = await PrepareWithInfoAsync(testDb, storage, musicXmlService);
        var before = DateTime.Now.AddSeconds(-2);

        var uploaded = Encoding.UTF8.GetBytes(
            "VERSION\t1\nCREATOR\tNewAuthor\nBPM_DEF\t210\nTAP\t0\t1\nTAP\t1\t2\nHLD\t0\t3\n");

        var result = await controller.UpdateOgkrFumen(
            CreateFormFile(uploaded, "master.ogkr"), 15.5f, 3, MusicId, Password);

        Assert.True(result.IsSuccess, result.Message);

        Assert.Equal(uploaded, await File.ReadAllBytesAsync(storage.GetArchivePath(MusicId, 3)));
        Assert.Contains("[自制谱 Lv.15] NewAuthor",
            await File.ReadAllTextAsync(storage.GetOptFumenPath(MusicId, 3)));

        var info = await ReadInfoAsync(storage);
        var masterInfo = info.FumenDifficults.First(d => d.DifficultIndex == 3);
        Assert.Equal(15.5f, masterInfo.Level);
        Assert.Equal("NewAuthor", masterInfo.Creator);
        Assert.Equal(210f, masterInfo.Bpm);
        Assert.Equal(2, masterInfo.TapCount);
        Assert.Equal(1, masterInfo.HoldCount);
        Assert.Equal(PublishState.NotReadyForPending, info.PublishState);
        Assert.True(info.UpdateTime >= before);

        var xmlText = await File.ReadAllTextAsync(Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml"));
        Assert.Contains("<FumenConstIntegerPart>15</FumenConstIntegerPart>", xmlText);
        Assert.Contains("<FumenConstFractionalPart>50</FumenConstFractionalPart>", xmlText);

        using var zip = ZipFile.OpenRead(storage.GetZipPath(MusicId));
        Assert.Contains("[自制谱 Lv.15] NewAuthor",
            ReadZipEntry(zip, $"music/music{MusicId:D4}/{MusicId:D4}_03.ogkr"));
        Assert.Contains("<FumenConstFractionalPart>50</FumenConstFractionalPart>",
            ReadZipEntry(zip, $"music/music{MusicId:D4}/Music.xml"));

        await using var db = testDb.Factory.CreateDbContext();
        var dbMaster = await db.FumenDifficults.FindAsync(MusicId, 3);
        Assert.NotNull(dbMaster);
        Assert.Equal(15.5f, dbMaster!.Level);
        Assert.Equal("NewAuthor", dbMaster.Creator);
    }

    [Fact]
    public async Task UpdateInfo_UsesInfoJsonAsSource_AndReRendersXmlOnRequest()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        using var env = UseFumenDirectory(temp.Path);
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var controller = await PrepareWithInfoAsync(testDb, storage, musicXmlService);

        var infoSet = CreateSeedSet();
        infoSet.Title = "Info Source Title";
        infoSet.FumenDifficults.First(d => d.DifficultIndex == 3).Level = 12.34f;
        var writeInfo = await storage.WriteInfoJsonAsync(infoSet);
        Assert.True(writeInfo.IsSuccess, writeInfo.Message);

        var staleSet = CreateSeedSet();
        staleSet.Title = "Stale XML Title";
        var xmlPath = Path.Combine(storage.GetOptMusicFolder(MusicId), "Music.xml");
        await musicXmlService.GenerateMusicXml(staleSet, xmlPath);

        var result = await controller.UpdateSetInfo(MusicId, Password, reGenerateMusicXml: false);
        Assert.True(result.IsSuccess, result.Message);

        var info = await ReadInfoAsync(storage);
        Assert.Equal("Info Source Title", info.Title);
        Assert.Equal(12.34f, info.FumenDifficults.First(d => d.DifficultIndex == 3).Level);
        Assert.Contains("Stale XML Title", await File.ReadAllTextAsync(xmlPath));

        await using (var db = testDb.Factory.CreateDbContext())
        {
            var dbSet = await db.FumenSets.FindAsync(MusicId);
            Assert.NotNull(dbSet);
            Assert.Equal("Info Source Title", dbSet!.Title);
        }

        result = await controller.UpdateSetInfo(MusicId, Password, reGenerateMusicXml: true);
        Assert.True(result.IsSuccess, result.Message);

        var rendered = await File.ReadAllTextAsync(xmlPath);
        Assert.Contains("Info Source Title", rendered);
        Assert.DoesNotContain("Stale XML Title", rendered);

        using var zip = ZipFile.OpenRead(storage.GetZipPath(MusicId));
        Assert.Contains("Info Source Title", ReadZipEntry(zip, $"music/music{MusicId:D4}/Music.xml"));
    }

    [Fact]
    public async Task UpdateRepackZip_RebuildsFromCurrentOpt_AndRefreshesInfo()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        using var env = UseFumenDirectory(temp.Path);
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var controller = await PrepareWithInfoAsync(testDb, storage, musicXmlService);
        var before = DateTime.Now.AddSeconds(-2);

        var changedChart = "VERSION\t1\nCREATOR\tChangedAuthor\nBPM_DEF\t150\n";
        await File.WriteAllTextAsync(storage.GetOptFumenPath(MusicId, 2), changedChart);

        var result = await controller.ForceRepackZipFile(MusicId, Password);

        Assert.True(result.IsSuccess, result.Message);

        using var zip = ZipFile.OpenRead(storage.GetZipPath(MusicId));
        Assert.Equal(changedChart, ReadZipEntry(zip, $"music/music{MusicId:D4}/{MusicId:D4}_02.ogkr"));
        Assert.Contains("Test Song", ReadZipEntry(zip, $"music/music{MusicId:D4}/Music.xml"));

        var info = await ReadInfoAsync(storage);
        Assert.Equal(PublishState.Pending, info.PublishState);
        Assert.True(info.UpdateTime >= before);

        await using var db = testDb.Factory.CreateDbContext();
        var dbSet = await db.FumenSets.FindAsync(MusicId);
        Assert.NotNull(dbSet);
        Assert.Equal(PublishState.Pending, dbSet!.PublishState);
        Assert.True(dbSet.UpdateTime >= before);
    }

    [Fact]
    public async Task UpdateAudio_ZipWithAcbAwb_UpdatesOptAndZip()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        using var env = UseFumenDirectory(temp.Path);
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var audioService = new AudioService(NullLogger<AudioService>.Instance);
        var controller = await PrepareWithInfoAsync(testDb, storage, musicXmlService, audioService: audioService);
        var before = DateTime.Now.AddSeconds(-2);

        var acbBytes = new byte[] { 1, 2, 3, 4 };
        var awbBytes = new byte[] { 5, 6, 7 };
        var zipBytes = CreateZip(("audio.acb", acbBytes), ("audio.awb", awbBytes));

        var result = await controller.UpdateAudio(
            CreateFormFile(zipBytes, "audio.zip"), MusicId, Password);

        Assert.True(result.IsSuccess, result.Message);

        var audioFolder = Path.Combine(storage.GetOptPath(MusicId), "musicsource", $"musicsource{MusicId:D4}");
        Assert.Equal(acbBytes, await File.ReadAllBytesAsync(Path.Combine(audioFolder, $"music{MusicId:D4}.acb")));
        Assert.Equal(awbBytes, await File.ReadAllBytesAsync(Path.Combine(audioFolder, $"music{MusicId:D4}.awb")));

        var sourceXml = await File.ReadAllTextAsync(Path.Combine(audioFolder, "MusicSource.xml"));
        Assert.Contains("Test Song", sourceXml);
        Assert.Contains($"music{MusicId:D4}.acb", sourceXml);
        Assert.Contains($"musicsource{MusicId:D4}", sourceXml);

        using var zip = ZipFile.OpenRead(storage.GetZipPath(MusicId));
        Assert.Equal(acbBytes, ReadZipEntryBytes(zip, $"musicsource/musicsource{MusicId:D4}/music{MusicId:D4}.acb"));
        Assert.Equal(awbBytes, ReadZipEntryBytes(zip, $"musicsource/musicsource{MusicId:D4}/music{MusicId:D4}.awb"));
        Assert.Contains("Test Song", ReadZipEntry(zip, $"musicsource/musicsource{MusicId:D4}/MusicSource.xml"));

        var info = await ReadInfoAsync(storage);
        Assert.Equal(PublishState.NotReadyForPending, info.PublishState);
        Assert.True(info.UpdateTime >= before);
    }

    [Fact]
    public async Task UpdateJacket_Png_GeneratesAssetsAndRefreshesInfo()
    {
        using var temp = new TempFolderScope();
        using var testDb = new TestDatabase();
        using var env = UseFumenDirectory(temp.Path);
        var storage = new FumenStorageService(temp.Path);
        var musicXmlService = new MusicXmlService(NullLogger<MusicXmlService>.Instance);
        var jacketService = new JacketService(NullLogger<JacketService>.Instance);
        var controller = await PrepareWithInfoAsync(testDb, storage, musicXmlService, jacketService: jacketService);
        var before = DateTime.Now.AddSeconds(-2);

        var pngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Zl1sAAAAASUVORK5CYII=");

        var result = await controller.UpdateJacket(
            CreateFormFile(pngBytes, "jacket.png"), MusicId, Password);

        Assert.True(result.IsSuccess, result.Message);

        Assert.Equal(pngBytes, await File.ReadAllBytesAsync(
            Path.Combine(storage.GetStoragePath(MusicId), "jacket.png")));

        var assetsFolder = Path.Combine(storage.GetOptPath(MusicId), "assets");
        Assert.NotEmpty(Directory.GetFiles(assetsFolder));

        using var zip = ZipFile.OpenRead(storage.GetZipPath(MusicId));
        Assert.NotEmpty(zip.Entries.Where(e => e.FullName.StartsWith("assets/")));

        var info = await ReadInfoAsync(storage);
        Assert.Equal(PublishState.NotReadyForPending, info.PublishState);
        Assert.True(info.UpdateTime >= before);
    }

    private static byte[] CreateZip(params (string Name, byte[] Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var stream = entry.Open();
                stream.Write(content);
            }
        }
        return ms.ToArray();
    }

    private static byte[] ReadZipEntryBytes(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName);
        Assert.NotNull(entry);
        using var stream = entry!.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static async Task WithAdminPassword(string password, Func<Task> action)
    {
        var previous = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD", password);

        try
        {
            await action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ADMIN_PASSWORD", previous);
        }
    }
}
