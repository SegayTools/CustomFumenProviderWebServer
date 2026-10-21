using CustomFumenProviderWebServer.Utils;

namespace CustomFumenProviderWebServer.Tests;

public class AudioFileTypesTests
{
    [Theory]
    [InlineData("audio.zip", true)]
    [InlineData("audio.ZIP", true)]
    [InlineData("audio.Zip", true)]
    [InlineData("audio.wav", false)]
    [InlineData("", false)]
    public void IsZip_IsCaseInsensitive(string fileName, bool expected)
    {
        Assert.Equal(expected, AudioFileTypes.IsZip(fileName));
    }

    [Fact]
    public void FindFileWithExtension_IsCaseInsensitive()
    {
        using var temp = new TempFolderScope();

        var acbPath = Path.Combine(temp.Path, "music001.ACB");
        var awbPath = Path.Combine(temp.Path, "music001.AWB");
        File.WriteAllText(acbPath, "acb");
        File.WriteAllText(awbPath, "awb");

        Assert.Equal(acbPath, AudioFileTypes.FindFileWithExtension(temp.Path, ".acb"));
        Assert.Equal(awbPath, AudioFileTypes.FindFileWithExtension(temp.Path, ".awb"));
        Assert.Null(AudioFileTypes.FindFileWithExtension(temp.Path, ".wav"));
    }

    [Fact]
    public void FindFileWithExtension_MatchesLowerCaseToo()
    {
        using var temp = new TempFolderScope();
        var acbPath = Path.Combine(temp.Path, "music001.acb");
        File.WriteAllText(acbPath, "acb");

        Assert.Equal(acbPath, AudioFileTypes.FindFileWithExtension(temp.Path, ".ACB"));
    }
}
