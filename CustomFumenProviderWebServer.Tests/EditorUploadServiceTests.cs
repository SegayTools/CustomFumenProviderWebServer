using System.IO.Compression;
using CustomFumenProviderWebServer.Services.Editor;

namespace CustomFumenProviderWebServer.Tests;

public class EditorUploadServiceTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Upload_NonMaster_UpdatesLatestOnly()
    {
        var editorService = new EditorService();
        var service = new EditorUploadService(editorService);
        var content = CreateZip();

        var result = service.Upload("OngekiFumenEditor_1.2.3_linux.zip", content, "develop", T0);

        Assert.True(result.IsSuccess);

        var latest = editorService.GetEditorResource(false);
        Assert.True(latest.IsAvaliable);
        Assert.Equal(content, latest.ZipContent);
        Assert.Equal("OngekiFumenEditor_1.2.3_linux.zip", latest.LocalFileName);
        Assert.Equal(new Version(1, 2, 3), latest.VersionInfo.Version);
        Assert.Equal("develop", latest.VersionInfo.Branch);
        Assert.Equal(T0, latest.VersionInfo.Time);
        Assert.Equal(content.Length, latest.VersionInfo.FileSize);

        Assert.False(editorService.GetEditorResource(true).IsAvaliable);
    }

    [Fact]
    public void Upload_Master_UpdatesMasterAndLatest()
    {
        var editorService = new EditorService();
        var service = new EditorUploadService(editorService);
        var content = CreateZip();

        var result = service.Upload("OngekiFumenEditor_2.0.0_windows.zip", content, "master", T0);

        Assert.True(result.IsSuccess);
        Assert.True(editorService.GetEditorResource(true).IsAvaliable);
        Assert.True(editorService.GetEditorResource(false).IsAvaliable);
        Assert.Equal(new Version(2, 0, 0), editorService.GetEditorResource(true).VersionInfo.Version);
    }

    [Theory]
    [InlineData("editor.zip")]
    [InlineData("OngekiFumenEditor_abc_windows.zip")]
    [InlineData("OngekiFumenEditor_1.2.3_windows.tar")]
    public void Upload_InvalidFileName_IsRejected(string fileName)
    {
        var editorService = new EditorService();
        var service = new EditorUploadService(editorService);

        var result = service.Upload(fileName, CreateZip(), "develop", T0);

        Assert.False(result.IsSuccess);
        Assert.False(editorService.GetEditorResource(false).IsAvaliable);
    }

    [Fact]
    public void Upload_EmptyContent_IsRejected()
    {
        var service = new EditorUploadService(new EditorService());

        var result = service.Upload("OngekiFumenEditor_1.2.3_windows.zip", [], "develop", T0);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Upload_SanitizesClientFileName()
    {
        var editorService = new EditorService();
        var service = new EditorUploadService(editorService);

        var result = service.Upload(@"C:\build\OngekiFumenEditor_1.0.0_windows.zip", CreateZip(), "develop", T0);

        Assert.True(result.IsSuccess);
        Assert.Equal("OngekiFumenEditor_1.0.0_windows.zip", editorService.GetEditorResource(false).LocalFileName);
    }

    [Fact]
    public void Upload_OlderMaster_DoesNotRegressLatest()
    {
        var editorService = new EditorService();
        var service = new EditorUploadService(editorService);

        service.Upload("OngekiFumenEditor_2.0.0_develop.zip", CreateZip(), "develop", T0.AddMinutes(1));
        service.Upload("OngekiFumenEditor_1.0.0_master.zip", CreateZip(), "master", T0);

        Assert.Equal(new Version(1, 0, 0), editorService.GetEditorResource(true).VersionInfo.Version);
        Assert.Equal(new Version(2, 0, 0), editorService.GetEditorResource(false).VersionInfo.Version);
    }

    [Fact]
    public void Upload_InvalidZipContent_IsRejected()
    {
        var service = new EditorUploadService(new EditorService());

        var result = service.Upload("OngekiFumenEditor_1.2.3_windows.zip", [1, 2, 3], "develop", T0);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Upload_OlderVersion_IsRejected()
    {
        var editorService = new EditorService();
        var service = new EditorUploadService(editorService);

        Assert.True(service.Upload("OngekiFumenEditor_2.0.0_develop.zip", CreateZip(), "develop", T0).IsSuccess);
        var result = service.Upload("OngekiFumenEditor_1.0.0_develop.zip", CreateZip(), "develop", T0.AddMinutes(1));

        Assert.False(result.IsSuccess);
        Assert.Equal(new Version(2, 0, 0), editorService.GetEditorResource(false).VersionInfo.Version);
    }

    [Fact]
    public void Upload_SameVersion_IsRejected()
    {
        var editorService = new EditorService();
        var service = new EditorUploadService(editorService);

        Assert.True(service.Upload("OngekiFumenEditor_1.0.0_develop.zip", CreateZip(), "develop", T0).IsSuccess);
        var result = service.Upload("OngekiFumenEditor_1.0.0_develop.zip", CreateZip(), "develop", T0.AddMinutes(1));

        Assert.False(result.IsSuccess);
        Assert.Equal(new Version(1, 0, 0), editorService.GetEditorResource(false).VersionInfo.Version);
    }

    [Fact]
    public void Upload_OlderMaster_IsRejected()
    {
        var editorService = new EditorService();
        var service = new EditorUploadService(editorService);

        Assert.True(service.Upload("OngekiFumenEditor_2.0.0_master.zip", CreateZip(), "master", T0).IsSuccess);
        var result = service.Upload("OngekiFumenEditor_1.0.0_master.zip", CreateZip(), "master", T0.AddMinutes(1));

        Assert.False(result.IsSuccess);
        Assert.Equal(new Version(2, 0, 0), editorService.GetEditorResource(true).VersionInfo.Version);
    }

    private static byte[] CreateZip()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var entry = zip.CreateEntry("test.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("test");
        }

        return ms.ToArray();
    }
}
