using System.Text;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Utils;

namespace CustomFumenProviderWebServer.Tests;

public class FumenChartServiceTests
{
    private static FumenChartService CreateService(string root)
        => new(new FumenStorageService(root), enableCreatorRewrite: true);

    [Theory]
    [InlineData(13.0f, "13")]
    [InlineData(13.69f, "13")]
    [InlineData(13.7f, "13+")]
    [InlineData(13.99f, "13+")]
    [InlineData(0f, "0")]
    public void FormatLevel_UsesGamePlusBorder70(float level, string expected)
    {
        Assert.Equal(expected, FumenChartService.FormatLevel(level));
    }

    [Fact]
    public async Task ProcessToFile_RewritesCreator_AndPreservesBomCrlfAndOtherLines()
    {
        using var temp = new TempFolderScope();
        var source = Path.Combine(temp.Path, "source.ogkr");
        var target = Path.Combine(temp.Path, "out", "master.ogkr");

        var text = "VERSION\t1\r\nCREATOR\tDJ.MSDP\r\nBPM_DEF\t180\r\nTAP\t0\t1\r\n";
        var body = Encoding.UTF8.GetBytes(text);
        var withBom = new byte[body.Length + 3];
        withBom[0] = 0xEF;
        withBom[1] = 0xBB;
        withBom[2] = 0xBF;
        body.CopyTo(withBom, 3);
        await File.WriteAllBytesAsync(source, withBom);

        var result = await CreateService(temp.Path).ProcessToFileAsync(source, target, 13.7f);

        Assert.True(result.IsSuccess, result.Message);
        var outputBytes = await File.ReadAllBytesAsync(target);
        Assert.Equal([0xEF, 0xBB, 0xBF], outputBytes[..3]);

        var output = Encoding.UTF8.GetString(outputBytes, 3, outputBytes.Length - 3);
        Assert.Equal(
            "VERSION\t1\r\nCREATOR\t[自制谱 Lv.13+] DJ.MSDP\r\nBPM_DEF\t180\r\nTAP\t0\t1\r\n",
            output);
    }

    [Fact]
    public async Task ProcessToFile_EmptyCreator_UsesDefaultDash()
    {
        using var temp = new TempFolderScope();
        var source = Path.Combine(temp.Path, "source.ogkr");
        var target = Path.Combine(temp.Path, "out.ogkr");
        await File.WriteAllTextAsync(source, "VERSION\t1\nCREATOR\t\nBPM_DEF\t180\n");

        var result = await CreateService(temp.Path).ProcessToFileAsync(source, target, 13.7f);

        Assert.True(result.IsSuccess, result.Message);
        var output = await File.ReadAllTextAsync(target);
        Assert.Contains("CREATOR\t[自制谱 Lv.13+] -\n", output);
    }

    [Fact]
    public async Task ProcessToFile_MissingCreator_InsertsAfterVersion()
    {
        using var temp = new TempFolderScope();
        var source = Path.Combine(temp.Path, "source.ogkr");
        var target = Path.Combine(temp.Path, "out.ogkr");
        await File.WriteAllTextAsync(source, "VERSION\t1\nBPM_DEF\t180\n");

        var result = await CreateService(temp.Path).ProcessToFileAsync(source, target, 13.7f);

        Assert.True(result.IsSuccess, result.Message);
        var output = await File.ReadAllTextAsync(target);
        Assert.Equal("VERSION\t1\nCREATOR\t[自制谱 Lv.13+] -\nBPM_DEF\t180\n", output);
    }

    [Fact]
    public async Task ProcessToFile_ExistingPrefix_IsReplacedInsteadOfStacked()
    {
        using var temp = new TempFolderScope();
        var source = Path.Combine(temp.Path, "source.ogkr");
        var target = Path.Combine(temp.Path, "out.ogkr");
        await File.WriteAllTextAsync(source, "VERSION\t1\nCREATOR\t[自制谱 Lv.12] DJ.MSDP\nBPM_DEF\t180\n");

        var result = await CreateService(temp.Path).ProcessToFileAsync(source, target, 13.7f);

        Assert.True(result.IsSuccess, result.Message);
        var output = await File.ReadAllTextAsync(target);
        Assert.Contains("CREATOR\t[自制谱 Lv.13+] DJ.MSDP\n", output);
        Assert.DoesNotContain("[自制谱 Lv.12]", output);
    }

    [Fact]
    public async Task ProcessToFile_OnlyFirstCreatorLineIsChanged()
    {
        using var temp = new TempFolderScope();
        var source = Path.Combine(temp.Path, "source.ogkr");
        var target = Path.Combine(temp.Path, "out.ogkr");
        await File.WriteAllTextAsync(source, "VERSION\t1\nCREATOR\tFirst\nBPM_DEF\t180\nCREATOR\tSecond\n");

        var result = await CreateService(temp.Path).ProcessToFileAsync(source, target, 13.7f);

        Assert.True(result.IsSuccess, result.Message);
        var output = await File.ReadAllTextAsync(target);
        Assert.Contains("CREATOR\t[自制谱 Lv.13+] First\n", output);
        Assert.Contains("CREATOR\tSecond\n", output);
    }

    [Fact]
    public async Task ProcessToFile_NonUtf8_FailsAndDoesNotCreateTarget()
    {
        using var temp = new TempFolderScope();
        var source = Path.Combine(temp.Path, "source.ogkr");
        var target = Path.Combine(temp.Path, "out.ogkr");
        await File.WriteAllBytesAsync(source, [0xFF, 0xFE, 0x00, 0x41]);

        var result = await CreateService(temp.Path).ProcessToFileAsync(source, target, 13.7f);

        Assert.False(result.IsSuccess);
        Assert.Contains("UTF-8", result.Message);
        Assert.False(File.Exists(target));
    }

    [Fact]
    public async Task ProcessToFile_UnrecognizableChart_Fails()
    {
        using var temp = new TempFolderScope();
        var source = Path.Combine(temp.Path, "source.ogkr");
        var target = Path.Combine(temp.Path, "out.ogkr");
        await File.WriteAllTextAsync(source, "hello\nworld\n");

        var result = await CreateService(temp.Path).ProcessToFileAsync(source, target, 13.7f);

        Assert.False(result.IsSuccess);
        Assert.Contains("CREATOR", result.Message);
        Assert.False(File.Exists(target));
    }

    [Fact]
    public async Task SaveChart_ArchivesRawBytesAndWritesProcessedOptCopy()
    {
        using var temp = new TempFolderScope();
        var storage = new FumenStorageService(temp.Path);
        var service = new FumenChartService(storage, true);

        var source = Path.Combine(temp.Path, "upload.ogkr");
        var raw = Encoding.UTF8.GetBytes("VERSION\t1\nCREATOR\tDJ.MSDP\nBPM_DEF\t180\n");
        await File.WriteAllBytesAsync(source, raw);

        var result = await service.SaveChartAsync(
            source,
            storage.GetArchivePath(25666, 3),
            storage.GetOptFumenPath(25666, 3),
            13.7f);

        Assert.True(result.IsSuccess, result.Message);

        var archive = await File.ReadAllBytesAsync(storage.GetArchivePath(25666, 3));
        Assert.Equal(raw, archive);
        Assert.EndsWith("master.ogkr", storage.GetArchivePath(25666, 3));

        var opt = await File.ReadAllTextAsync(storage.GetOptFumenPath(25666, 3));
        Assert.Contains("CREATOR\t[自制谱 Lv.13+] DJ.MSDP", opt);
        Assert.EndsWith(Path.Combine("opt", "music", "music25666", "25666_03.ogkr"), storage.GetOptFumenPath(25666, 3));
    }

    [Fact]
    public async Task SaveDeliverCharts_WritesArchivesAtRootAndProcessedCopiesInOpt()
    {
        using var temp = new TempFolderScope();
        var storage = new FumenStorageService(temp.Path);
        var service = new FumenChartService(storage, true);

        var archiveFolder = Path.Combine(temp.Path, "generated");
        var optMusicFolder = Path.Combine(archiveFolder, "opt", "music", "music25666");
        Directory.CreateDirectory(archiveFolder);
        Directory.CreateDirectory(optMusicFolder);

        var charts = new List<FumenChartUpload>();
        for (int diffIdx = 0; diffIdx < 5; diffIdx++)
        {
            var source = Path.Combine(temp.Path, $"upload{diffIdx}.ogkr");
            await File.WriteAllTextAsync(source, $"VERSION\t1\nCREATOR\tAuthor{diffIdx}\nBPM_DEF\t180\n");
            charts.Add(new FumenChartUpload(diffIdx, source, 10 + diffIdx));
        }

        var result = await service.SaveDeliverChartsAsync(25666, archiveFolder, optMusicFolder, charts);

        Assert.True(result.IsSuccess, result.Message);

        var archiveNames = new[] { "basic.ogkr", "advanced.ogkr", "expert.ogkr", "master.ogkr", "lunatic.ogkr" };
        for (int diffIdx = 0; diffIdx < 5; diffIdx++)
        {
            var archive = await File.ReadAllTextAsync(Path.Combine(archiveFolder, archiveNames[diffIdx]));
            Assert.Contains($"CREATOR\tAuthor{diffIdx}", archive);
            Assert.DoesNotContain("[自制谱", archive);

            var opt = await File.ReadAllTextAsync(Path.Combine(optMusicFolder, $"25666_0{diffIdx}.ogkr"));
            Assert.Contains($"CREATOR\t[自制谱 Lv.{10 + diffIdx}] Author{diffIdx}", opt);
        }
    }

    [Fact]
    public async Task RegenerateOptChart_PrefersArchive_WhenBothExist()
    {
        using var temp = new TempFolderScope();
        var storage = new FumenStorageService(temp.Path);
        var service = new FumenChartService(storage, true);

        var archive = storage.GetArchivePath(25666, 3);
        var opt = storage.GetOptFumenPath(25666, 3);
        Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
        Directory.CreateDirectory(Path.GetDirectoryName(opt)!);
        await File.WriteAllTextAsync(archive, "VERSION\t1\nCREATOR\tArchiveAuthor\nBPM_DEF\t180\n");
        await File.WriteAllTextAsync(opt, "VERSION\t1\nCREATOR\t[自制谱 Lv.1] StaleAuthor\nBPM_DEF\t180\n");

        var result = await service.RegenerateOptChartAsync(25666, 3, 13.7f);

        Assert.True(result.IsSuccess, result.Message);
        var output = await File.ReadAllTextAsync(opt);
        Assert.Contains("CREATOR\t[自制谱 Lv.13+] ArchiveAuthor", output);
        Assert.DoesNotContain("StaleAuthor", output);
    }

    [Fact]
    public async Task RegenerateOptChart_WithoutArchive_UsesExistingOptFile()
    {
        using var temp = new TempFolderScope();
        var storage = new FumenStorageService(temp.Path);
        var service = new FumenChartService(storage, true);

        var opt = storage.GetOptFumenPath(25666, 3);
        Directory.CreateDirectory(Path.GetDirectoryName(opt)!);
        await File.WriteAllTextAsync(opt, "VERSION\t1\nCREATOR\t[自制谱 Lv.1] LegacyAuthor\nBPM_DEF\t180\n");

        var result = await service.RegenerateOptChartAsync(25666, 3, 14.7f);

        Assert.True(result.IsSuccess, result.Message);
        var output = await File.ReadAllTextAsync(opt);
        Assert.Contains("CREATOR\t[自制谱 Lv.14+] LegacyAuthor", output);
    }

    [Fact]
    public async Task ProcessToFile_WhenRewriteDisabled_CopiesUploadVerbatim()
    {
        using var temp = new TempFolderScope();
        var storage = new FumenStorageService(temp.Path);
        var service = new FumenChartService(storage, enableCreatorRewrite: false);

        var source = Path.Combine(temp.Path, "source.ogkr");
        var target = Path.Combine(temp.Path, "out.ogkr");
        var body = Encoding.UTF8.GetBytes("VERSION\t1\r\nCREATOR\tDJ.MSDP\r\nBPM_DEF\t180\r\n");
        var raw = new byte[body.Length + 3];
        raw[0] = 0xEF;
        raw[1] = 0xBB;
        raw[2] = 0xBF;
        body.CopyTo(raw, 3);
        await File.WriteAllBytesAsync(source, raw);

        var result = await service.ProcessToFileAsync(source, target, 13.7f);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(raw, await File.ReadAllBytesAsync(target));
        Assert.DoesNotContain("[自制谱", await File.ReadAllTextAsync(target));
    }

    [Fact]
    public async Task SaveChart_WhenRewriteDisabled_OptIsRawCopy()
    {
        using var temp = new TempFolderScope();
        var storage = new FumenStorageService(temp.Path);
        var service = new FumenChartService(storage, enableCreatorRewrite: false);

        var source = Path.Combine(temp.Path, "upload.ogkr");
        var raw = Encoding.UTF8.GetBytes("VERSION\t1\nCREATOR\tDJ.MSDP\nBPM_DEF\t180\n");
        await File.WriteAllBytesAsync(source, raw);

        var result = await service.SaveChartAsync(
            source,
            storage.GetArchivePath(25666, 3),
            storage.GetOptFumenPath(25666, 3),
            13.7f);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(raw, await File.ReadAllBytesAsync(storage.GetArchivePath(25666, 3)));
        Assert.Equal(raw, await File.ReadAllBytesAsync(storage.GetOptFumenPath(25666, 3)));
    }

    [Fact]
    public async Task RegenerateOptChart_WhenRewriteDisabled_DoesNotTouchFile()
    {
        using var temp = new TempFolderScope();
        var storage = new FumenStorageService(temp.Path);
        var service = new FumenChartService(storage, enableCreatorRewrite: false);

        var opt = storage.GetOptFumenPath(25666, 3);
        Directory.CreateDirectory(Path.GetDirectoryName(opt)!);
        var original = "VERSION\t1\nCREATOR\tDJ.MSDP\nBPM_DEF\t180\n";
        await File.WriteAllTextAsync(opt, original);

        var result = await service.RegenerateOptChartAsync(25666, 3, 13.7f);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(original, await File.ReadAllTextAsync(opt));
    }
}
