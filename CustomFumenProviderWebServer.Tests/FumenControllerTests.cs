using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using CustomFumenProviderWebServer.Controllers;
using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

    private static FormFile CreateFormFile(byte[] bytes)
    {
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "ogkrFormFile", "upload-name.ogkr");
    }

    private static async Task<FumenController> PrepareAsync(
        TestDatabase testDb,
        FumenStorageService storage,
        MusicXmlService musicXmlService)
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

        return CreateController(testDb, storage, musicXmlService);
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

    private static FumenController CreateController(TestDatabase testDb, FumenStorageService storage, MusicXmlService musicXmlService)
    {
        var controller = new FumenController(
            NullLogger<FumenController>.Instance,
            testDb.Factory,
            null!,
            null!,
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
