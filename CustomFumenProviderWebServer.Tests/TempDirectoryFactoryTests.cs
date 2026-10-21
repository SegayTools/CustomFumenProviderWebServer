using CustomFumenProviderWebServer.Utils;

namespace CustomFumenProviderWebServer.Tests;

public class TempDirectoryFactoryTests
{
    [Fact]
    public void Create_ReturnsNewExistingDirectories()
    {
        var first = TempDirectoryFactory.Create("fumen-test");
        var second = TempDirectoryFactory.Create("fumen-test");

        try
        {
            Assert.True(Directory.Exists(first));
            Assert.True(Directory.Exists(second));
            Assert.NotEqual(first, second);
            Assert.False(File.Exists(first));
            Assert.StartsWith("fumen-test_", Path.GetFileName(first));
        }
        finally
        {
            if (Directory.Exists(first))
                Directory.Delete(first, true);
            if (Directory.Exists(second))
                Directory.Delete(second, true);
        }
    }
}
