namespace CustomFumenProviderWebServer.Services.Editor
{
    public interface IEditorService
    {
        EditorResource GetEditorResource(bool requireMasterBranch);

        void UpdateEditorResource(bool requireMasterBranch, EditorResource resource);
    }
}
