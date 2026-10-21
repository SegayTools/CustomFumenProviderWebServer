using CustomFumenProviderWebServer.Services.Editor;

namespace CustomFumenProviderWebServer.Tests;

public class EditorArtifactNameTests
{
    [Theory]
    [InlineData("OngekiFumenEditor_1.2.3_windows", true, "1.2.3")]
    [InlineData("OngekiFumenEditor_1.2_windows", true, "1.2")]
    [InlineData("OngekiFumenEditor_1.2.3.4_linux", true, "1.2.3.4")]
    [InlineData("OngekiFumenEditor_abc_windows", false, null)]
    [InlineData("Other_1.2.3_windows", false, null)]
    public void TryParse_ParsesVersionFromExpectedNames(string name, bool expected, string versionText)
    {
        var success = EditorArtifactName.TryParse(name, out var version);

        Assert.Equal(expected, success);
        if (expected)
            Assert.Equal(Version.Parse(versionText), version);
        else
            Assert.Null(version);
    }

    [Theory]
    [InlineData("package.zip", true)]
    [InlineData("package.ZIP", true)]
    [InlineData("package.tar", false)]
    [InlineData("package", false)]
    public void IsZipFileName_IsCaseInsensitive(string fileName, bool expected)
    {
        Assert.Equal(expected, EditorArtifactName.IsZipFileName(fileName));
    }

    [Theory]
    [InlineData(@"C:\build\OngekiFumenEditor_1.0.0_windows.zip", "OngekiFumenEditor_1.0.0_windows.zip")]
    [InlineData("/tmp/build/OngekiFumenEditor_1.0.0_windows.zip", "OngekiFumenEditor_1.0.0_windows.zip")]
    [InlineData("OngekiFumenEditor_1.0.0_windows.zip", "OngekiFumenEditor_1.0.0_windows.zip")]
    public void SanitizeFileName_RemovesDirectory(string input, string expected)
    {
        Assert.Equal(expected, EditorArtifactName.SanitizeFileName(input));
    }
}
