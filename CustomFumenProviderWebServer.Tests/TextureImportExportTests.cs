using AssetsTools.NET;
using CustomFumenProviderWebServer.Utils;
using TexturePlugin;

namespace CustomFumenProviderWebServer.Tests;

public class TextureImportExportTests
{
    [Fact]
    public void ExportPng_WritesPngForDxt1Texture()
    {
        using var temp = new TempFolderScope();
        var outputPath = Path.Combine(temp.Path, "jacket.png");

        // 4x4 DXT1 block (8 bytes). All-zero data decodes to a valid 4x4 image.
        var textureData = new byte[8];

        var result = TextureImportExport.ExportPng(textureData, outputPath, 4, 4, TextureFormat.DXT1);

        Assert.True(result);
        Assert.True(File.Exists(outputPath));

        var header = new byte[8];
        using (var fs = File.OpenRead(outputPath))
            fs.ReadExactly(header);

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, header);
    }

    [Fact]
    public void ExportPng_UnsupportedFormat_ReturnsFalse()
    {
        using var temp = new TempFolderScope();
        var outputPath = Path.Combine(temp.Path, "jacket.png");

        var result = TextureImportExport.ExportPng([], outputPath, 4, 4, TextureFormat.BC6H);

        Assert.False(result);
        Assert.False(File.Exists(outputPath));
    }
}
