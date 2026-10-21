using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Services.Editor;

namespace CustomFumenProviderWebServer.Tests;

public class EditorArtifactSelectorTests
{
    private static GithubActionArtifact CreateArtifact(string name, bool expired = false)
    {
        return new GithubActionArtifact
        {
            Name = name,
            ArchiveDownloadUrl = $"https://example.invalid/{name}",
            Expired = expired,
        };
    }

    [Fact]
    public void TrySelect_SkipsUnrelatedArtifacts()
    {
        var editor = CreateArtifact("OngekiFumenEditor_0.9.8.311_master_(GABuild20261022001021_4c9bff_Release)");
        var artifacts = new[]
        {
            CreateArtifact("browser-site-mt"),
            CreateArtifact("github-pages"),
            editor,
        };

        var selected = EditorArtifactSelector.TrySelect(artifacts, out var artifact, out var version);

        Assert.True(selected);
        Assert.Same(editor, artifact);
        Assert.Equal(Version.Parse("0.9.8.311"), version);
    }

    [Fact]
    public void TrySelect_SkipsExpiredArtifacts()
    {
        var available = CreateArtifact("OngekiFumenEditor_0.9.8.311_master");
        var artifacts = new[]
        {
            CreateArtifact("OngekiFumenEditor_0.9.8.310_master", expired: true),
            available,
        };

        var selected = EditorArtifactSelector.TrySelect(artifacts, out var artifact, out var version);

        Assert.True(selected);
        Assert.Same(available, artifact);
        Assert.Equal(Version.Parse("0.9.8.311"), version);
    }

    [Fact]
    public void TrySelect_NoEditorArtifact_ReturnsFalse()
    {
        var artifacts = new[]
        {
            CreateArtifact("browser-site-mt"),
            CreateArtifact("github-pages"),
        };

        Assert.False(EditorArtifactSelector.TrySelect(artifacts, out var artifact, out var version));
        Assert.Null(artifact);
        Assert.Null(version);
    }

    [Fact]
    public void TrySelect_NullList_ReturnsFalse()
    {
        Assert.False(EditorArtifactSelector.TrySelect(null, out var artifact, out var version));
        Assert.Null(artifact);
        Assert.Null(version);
    }
}
