using CustomFumenProviderWebServer.Services.Editor;

namespace CustomFumenProviderWebServer.Tests;

public class EditorServiceTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static EditorResource CreateResource(DateTime time, string name, string version)
    {
        return new EditorResource
        {
            LocalFileName = name,
            ZipContent = [1, 2, 3],
            LocalCreateAt = time,
            VersionInfo = new VersionInfo
            {
                Time = time,
                Branch = "branch",
                Version = Version.Parse(version),
            },
        };
    }

    [Fact]
    public void UpdateLatest_DoesNotTouchMaster()
    {
        var service = new EditorService();
        var resource = CreateResource(T0, "latest.zip", "1.0.0");

        Assert.True(service.UpdateEditorResource(false, resource));

        Assert.Same(resource, service.GetEditorResource(false));
        Assert.False(service.GetEditorResource(true).IsAvaliable);
    }

    [Fact]
    public void UpdateMaster_AlsoUpdatesNewerLatest()
    {
        var service = new EditorService();
        var resource = CreateResource(T0, "master.zip", "1.0.0");

        Assert.True(service.UpdateEditorResource(true, resource));

        Assert.Same(resource, service.GetEditorResource(true));
        Assert.Same(resource, service.GetEditorResource(false));
    }

    [Fact]
    public void OlderVersion_DoesNotOverwriteNewerLatest()
    {
        var service = new EditorService();
        var newer = CreateResource(T0, "newer.zip", "2.0.0");
        var older = CreateResource(T0.AddMinutes(1), "older.zip", "1.0.0");

        Assert.True(service.UpdateEditorResource(false, newer));
        Assert.False(service.UpdateEditorResource(false, older));

        Assert.Same(newer, service.GetEditorResource(false));
    }

    [Fact]
    public void SameVersion_DoesNotOverwriteLatest()
    {
        var service = new EditorService();
        var first = CreateResource(T0, "first.zip", "1.2.3");
        var second = CreateResource(T0.AddMinutes(1), "second.zip", "1.2.3");

        Assert.True(service.UpdateEditorResource(false, first));
        Assert.False(service.UpdateEditorResource(false, second));

        Assert.Same(first, service.GetEditorResource(false));
    }

    [Fact]
    public void OlderMaster_UpdatesMasterWithoutRegressingLatest()
    {
        var service = new EditorService();
        var newerLatest = CreateResource(T0.AddMinutes(1), "latest.zip", "2.0.0");
        var olderMaster = CreateResource(T0, "master.zip", "1.0.0");

        Assert.True(service.UpdateEditorResource(false, newerLatest));
        Assert.True(service.UpdateEditorResource(true, olderMaster));

        Assert.Same(olderMaster, service.GetEditorResource(true));
        Assert.Same(newerLatest, service.GetEditorResource(false));
    }

    [Fact]
    public void OlderMaster_DoesNotOverwriteCurrentMaster()
    {
        var service = new EditorService();
        var newerMaster = CreateResource(T0, "master.zip", "2.0.0");
        var olderMaster = CreateResource(T0.AddMinutes(1), "old-master.zip", "1.0.0");

        Assert.True(service.UpdateEditorResource(true, newerMaster));
        Assert.False(service.UpdateEditorResource(true, olderMaster));

        Assert.Same(newerMaster, service.GetEditorResource(true));
    }
}
