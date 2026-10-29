using System.Text;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Utils;

namespace CustomFumenProviderWebServer.Tests;

public class FumenXmlServiceTests
{
    [Fact]
    public async Task ArchiveProvidedXmlAsync_ByMusicId_WritesLowercaseMusicXmlAtFumenRoot()
    {
        using var temp = new TempFolderScope();
        var storage = new FumenStorageService(temp.Path);
        var service = new FumenXmlService(storage);

        var source = Path.Combine(temp.Path, "upload.xml");
        var bytes = Encoding.UTF8.GetBytes("<MusicData><Name><str>Test</str></Name></MusicData>");
        await File.WriteAllBytesAsync(source, bytes);

        var result = await service.ArchiveProvidedXmlAsync(25666, source);

        Assert.True(result.IsSuccess, result.Message);
        var archivePath = storage.GetArchiveXmlPath(25666);
        Assert.EndsWith(Path.Combine("fumen25666", "music.xml"), archivePath);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(archivePath));
    }

    [Fact]
    public async Task ArchiveProvidedXmlAsync_ByFolder_WritesIntoGeneratedFolder()
    {
        using var temp = new TempFolderScope();
        var service = new FumenXmlService(new FumenStorageService(temp.Path));

        var source = Path.Combine(temp.Path, "upload.xml");
        var bytes = Encoding.UTF8.GetBytes("<MusicData/>");
        await File.WriteAllBytesAsync(source, bytes);

        var generatedFolder = Path.Combine(temp.Path, "generated");
        Directory.CreateDirectory(generatedFolder);

        var result = await service.ArchiveProvidedXmlAsync(generatedFolder, source);

        Assert.True(result.IsSuccess, result.Message);
        var archivePath = Path.Combine(generatedFolder, "music.xml");
        Assert.True(File.Exists(archivePath));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(archivePath));
    }

    [Fact]
    public async Task ArchiveProvidedXmlAsync_OverwritesPreviousArchive()
    {
        using var temp = new TempFolderScope();
        var storage = new FumenStorageService(temp.Path);
        var service = new FumenXmlService(storage);

        var first = Path.Combine(temp.Path, "first.xml");
        var second = Path.Combine(temp.Path, "second.xml");
        await File.WriteAllTextAsync(first, "<old/>");
        await File.WriteAllTextAsync(second, "<new/>");

        Assert.True((await service.ArchiveProvidedXmlAsync(25666, first)).IsSuccess);
        Assert.True((await service.ArchiveProvidedXmlAsync(25666, second)).IsSuccess);

        Assert.Equal("<new/>", await File.ReadAllTextAsync(storage.GetArchiveXmlPath(25666)));
    }

    [Fact]
    public async Task ArchiveProvidedXmlAsync_MissingSource_ReturnsFailure()
    {
        using var temp = new TempFolderScope();
        var storage = new FumenStorageService(temp.Path);
        var service = new FumenXmlService(storage);

        var result = await service.ArchiveProvidedXmlAsync(25666, Path.Combine(temp.Path, "missing.xml"));

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(storage.GetArchiveXmlPath(25666)));
    }
}
