using CustomFumenProviderWebServer.Models;

namespace CustomFumenProviderWebServer.Services.Editor
{
    /// <summary>
    /// 从一次 workflow run 的 artifact 列表里挑出编辑器包。
    /// 同一次构建可能附带与编辑器无关的产物（如部署浏览器的 browser-site-mt），不能直接取第一个。
    /// </summary>
    public static class EditorArtifactSelector
    {
        public static bool TrySelect(IEnumerable<GithubActionArtifact> artifacts, out GithubActionArtifact artifact, out Version version)
        {
            artifact = null;
            version = null;

            if (artifacts is null)
                return false;

            foreach (var candidate in artifacts)
            {
                if (candidate is null || candidate.Expired)
                    continue;
                if (!EditorArtifactName.TryParse(candidate.Name, out var parsed))
                    continue;

                artifact = candidate;
                version = parsed;
                return true;
            }

            return false;
        }
    }
}
