using CustomFumenProviderWebServer.Services.Editor;

namespace CustomFumenProviderWebServer.Tests;

public class EditorServiceTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static EditorResource CreateResource(DateTime time, string name)
    {
        return new EditorResource
        {
            LocalFileName = name,
            ZipContent = [1, 2, 3],
            LocalCreateAt = time,
            VersionInfo = new VersionInfo { Time = time, Branch = "branch" },
        };
    }

    [Fact]
    public void UpdateLatest_DoesNotTouchMaster()
    {
        var service = new EditorService();
        var resource = CreateResource(T0, "latest.zip");

        service.UpdateEditorResource(false, resource);

        Assert.Same(resource, service.GetEditorResource(false));
        Assert.False(service.GetEditorResource(true).IsAvaliable);
    }

    [Fact]
    public void UpdateMaster_AlsoUpdatesNewerLatest()
    {
        var service = new EditorService();
        var resource = CreateResource(T0, "master.zip");

        service.UpdateEditorResource(true, resource);

        Assert.Same(resource, service.GetEditorResource(true));
        Assert.Same(resource, service.GetEditorResource(false));
    }

    [Fact]
    public void OlderLatest_DoesNotOverwriteNewerLatest()
    {
        var service = new EditorService();
        var newer = CreateResource(T0.AddMinutes(1), "newer.zip");
        var older = CreateResource(T0, "older.zip");

        service.UpdateEditorResource(false, newer);
        service.UpdateEditorResource(false, older);

        Assert.Same(newer, service.GetEditorResource(false));
    }

    [Fact]
    public void OlderMaster_UpdatesMasterWithoutRegressingLatest()
    {
        var service = new EditorService();
        var newerLatest = CreateResource(T0.AddMinutes(1), "latest.zip");
        var olderMaster = CreateResource(T0, "master.zip");

        service.UpdateEditorResource(false, newerLatest);
        service.UpdateEditorResource(true, olderMaster);

        Assert.Same(olderMaster, service.GetEditorResource(true));
        Assert.Same(newerLatest, service.GetEditorResource(false));
    }
}
