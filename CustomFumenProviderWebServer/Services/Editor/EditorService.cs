namespace CustomFumenProviderWebServer.Services.Editor
{
    public class EditorService : IEditorService
    {
        private readonly object sync = new();

        private EditorResource latestEditorResource = new();
        private EditorResource masterEditorResource = new();

        public EditorResource GetEditorResource(bool requireMasterBranch)
        {
            lock (sync)
            {
                return requireMasterBranch ? masterEditorResource : latestEditorResource;
            }
        }

        /// <summary>
        /// 以引用替换的方式发布新资源，读取方不会看到半更新的对象。
        /// </summary>
        public void UpdateEditorResource(bool requireMasterBranch, EditorResource resource)
        {
            lock (sync)
            {
                if (requireMasterBranch && resource.LocalCreateAt >= masterEditorResource.LocalCreateAt)
                    masterEditorResource = resource;

                if (resource.LocalCreateAt >= latestEditorResource.LocalCreateAt)
                    latestEditorResource = resource;
            }
        }
    }
}
