namespace CustomFumenProviderWebServer.Services.Editor
{
    public interface IEditorService
    {
        EditorResource GetEditorResource(bool requireMasterBranch);

        /// <summary>
        /// 仅当 resource 的版本号高于目标当前版本时才发布；返回是否至少更新了一个通道。
        /// </summary>
        bool UpdateEditorResource(bool requireMasterBranch, EditorResource resource);
    }
}
