namespace CustomFumenProviderWebServer.Services.Editor
{
    public interface IEditorService
    {
        EditorResource GetEditorResource(bool requireMasterBranch);
    }
}
