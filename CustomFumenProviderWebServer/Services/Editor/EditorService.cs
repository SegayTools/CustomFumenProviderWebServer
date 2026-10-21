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
        /// 只有版本号严格高于目标当前版本时才会覆盖，避免旧版本回退。
        /// </summary>
        public bool UpdateEditorResource(bool requireMasterBranch, EditorResource resource)
        {
            lock (sync)
            {
                var updated = false;

                if (requireMasterBranch && IsNewer(resource, masterEditorResource))
                {
                    masterEditorResource = resource;
                    updated = true;
                }

                if (IsNewer(resource, latestEditorResource))
                {
                    latestEditorResource = resource;
                    updated = true;
                }

                return updated;
            }
        }

        private static bool IsNewer(EditorResource candidate, EditorResource current)
        {
            var candidateVersion = candidate.VersionInfo?.Version;
            if (candidateVersion is null)
                return false;

            var currentVersion = current.VersionInfo?.Version;
            if (currentVersion is null)
                return true;

            return candidateVersion.CompareTo(currentVersion) > 0;
        }
    }
}
