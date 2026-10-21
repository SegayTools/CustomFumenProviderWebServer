using CustomFumenProviderWebServer.Utils;

namespace CustomFumenProviderWebServer.Tests;

public class TempFolderScopeTests
{
    [Fact]
    public void Constructor_CreatesExistingDirectory()
    {
        using var scope = new TempFolderScope();

        Assert.True(Directory.Exists(scope.Path));
    }

    [Fact]
    public void Dispose_DeletesDirectoryWithNestedContent()
    {
        string path;

        using (var scope = new TempFolderScope())
        {
            path = scope.Path;
            var nested = Path.Combine(path, "a", "b");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "file.txt"), "content");
        }

        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var scope = new TempFolderScope();
        var path = scope.Path;

        scope.Dispose();
        scope.Dispose();

        Assert.False(Directory.Exists(path));
    }
}
