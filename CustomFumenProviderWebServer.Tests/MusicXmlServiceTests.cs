using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomFumenProviderWebServer.Tests;

public class MusicXmlServiceTests
{
    private static MusicXmlService CreateService()
        => new(NullLogger<MusicXmlService>.Instance);

    [Fact]
    public async Task ParseFumenFileInfo_StripsSelfMadePrefixFromCreator()
    {
        using var temp = new TempFolderScope();
        var chart = Path.Combine(temp.Path, "master.ogkr");
        await File.WriteAllTextAsync(chart, "VERSION\t1\nCREATOR\t[自制谱 Lv.13+] DJ.MSDP\nBPM_DEF\t180\n");

        var diff = new FumenDifficult();
        var result = await CreateService().ParseFumenFileInfo(diff, chart);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("DJ.MSDP", diff.Creator);
        Assert.Equal(180f, diff.Bpm);
    }

    [Fact]
    public async Task ParseFumenFileInfo_BlankCreator_FallsBackToDefaultDash()
    {
        using var temp = new TempFolderScope();
        var chart = Path.Combine(temp.Path, "master.ogkr");
        await File.WriteAllTextAsync(chart, "VERSION\t1\nCREATOR\t\nBPM_DEF\t180\n");

        var diff = new FumenDifficult();
        var result = await CreateService().ParseFumenFileInfo(diff, chart);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("-", diff.Creator);
    }

    [Fact]
    public async Task ParseFumenFileInfo_DoesNotMatchCreatorLikeTag()
    {
        using var temp = new TempFolderScope();
        var chart = Path.Combine(temp.Path, "master.ogkr");
        await File.WriteAllTextAsync(chart, "VERSION\t1\nCREATOR_NOT_A_TAG\tvalue\nBPM_DEF\t180\n");

        var diff = new FumenDifficult();
        var result = await CreateService().ParseFumenFileInfo(diff, chart);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Null(diff.Creator);
    }
}
