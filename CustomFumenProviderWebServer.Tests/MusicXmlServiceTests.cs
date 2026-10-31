using System.Xml.Linq;
using System.Xml.XPath;
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
    public async Task GenerateMusicXml_ZeroLevels_WritesZeroConstsAndKeepsPaths()
    {
        using var temp = new TempFolderScope();
        var set = CreateSet();

        var zeroPath = Path.Combine(temp.Path, "zero.xml");
        var zeroResult = await CreateService().GenerateMusicXml(set, zeroPath, zeroLevels: true);
        Assert.True(zeroResult.IsSuccess, zeroResult.Message);

        var zeroXml = XDocument.Load(zeroPath);
        var master = zeroXml.XPathSelectElement("/MusicData/FumenData/FumenData[4]");
        Assert.NotNull(master);
        Assert.Equal("0", master!.Element("FumenConstIntegerPart")?.Value);
        Assert.Equal("0", master.Element("FumenConstFractionalPart")?.Value);
        Assert.Equal("20001_03.ogkr", master.XPathSelectElement("./FumenFile/path")?.Value);

        var realPath = Path.Combine(temp.Path, "real.xml");
        var realResult = await CreateService().GenerateMusicXml(set, realPath);
        Assert.True(realResult.IsSuccess, realResult.Message);

        var realMaster = XDocument.Load(realPath).XPathSelectElement("/MusicData/FumenData/FumenData[4]");
        Assert.NotNull(realMaster);
        Assert.Equal("14", realMaster!.Element("FumenConstIntegerPart")?.Value);
        Assert.Equal("75", realMaster.Element("FumenConstFractionalPart")?.Value);
        Assert.Equal("20001_03.ogkr", realMaster.XPathSelectElement("./FumenFile/path")?.Value);
    }

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

    [Theory]
    [InlineData(13.7f, 1370)]
    [InlineData(13.695f, 1370)]
    [InlineData(13.694f, 1369)]
    [InlineData(0f, 0)]
    public void LevelToHundredths_RoundsAwayFromZero(float level, int expected)
    {
        Assert.Equal(expected, MusicXmlService.LevelToHundredths(level));
    }

    [Fact]
    public async Task GenerateMusicXml_ZeroLevels_LunaticWritesFifthPath()
    {
        using var temp = new TempFolderScope();
        var set = CreateSet(5);
        var path = Path.Combine(temp.Path, "lunatic.xml");

        var result = await CreateService().GenerateMusicXml(set, path, zeroLevels: true);

        Assert.True(result.IsSuccess, result.Message);
        var musicXml = XDocument.Load(path);
        var lunatic = musicXml.XPathSelectElement("/MusicData/FumenData/FumenData[5]");
        Assert.NotNull(lunatic);
        Assert.Equal("0", lunatic!.Element("FumenConstIntegerPart")?.Value);
        Assert.Equal("0", lunatic.Element("FumenConstFractionalPart")?.Value);
        Assert.Equal("20001_04.ogkr", lunatic.XPathSelectElement("./FumenFile/path")?.Value);
        Assert.Equal("true", musicXml.XPathSelectElement("/MusicData/IsLunatic")?.Value);
    }

    [Fact]
    public async Task GenerateMusicXml_NonLunaticMissingDifficulty_FailsBeforeWriting()
    {
        using var temp = new TempFolderScope();
        var set = CreateSet(3);
        var path = Path.Combine(temp.Path, "missing.xml");

        var result = await CreateService().GenerateMusicXml(set, path);

        Assert.False(result.IsSuccess);
        Assert.Contains("必须提供", result.Message);
        Assert.False(File.Exists(path));
    }

    private static FumenSet CreateSet(int difficultCount = 4)
    {
        var set = new FumenSet
        {
            MusicId = 20001,
            Title = "Zero Test",
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
                MusicId = set.MusicId,
                DifficultIndex = i,
                Level = 10 + i,
            });
        }

        if (difficultCount > 3)
            set.FumenDifficults.First(d => d.DifficultIndex == 3).Level = 14.75f;

        return set;
    }
}
