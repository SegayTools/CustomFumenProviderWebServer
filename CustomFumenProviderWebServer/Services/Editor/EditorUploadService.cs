using CustomFumenProviderWebServer.Models;

namespace CustomFumenProviderWebServer.Services.Editor
{
    /// <summary>
    /// 接收 GitHub Action 主动推送的编辑器 zip。
    /// </summary>
    public class EditorUploadService
    {
        private readonly IEditorService editorService;

        public EditorUploadService(IEditorService editorService)
        {
            this.editorService = editorService;
        }

        public Result Upload(string fileName, byte[] content, string branch, DateTime uploadedAt)
        {
            if (content is null || content.Length == 0)
                return new(false, "zip file is empty");

            var safeFileName = EditorArtifactName.SanitizeFileName(fileName);
            if (!EditorArtifactName.IsZipFileName(safeFileName))
                return new(false, "file must be a .zip");
            if (!EditorArtifactName.TryParse(safeFileName, out var version))
                return new(false, "file name must match OngekiFumenEditor_<version>_*.zip");
            if (!EditorZip.IsValid(content))
                return new(false, "file is not a valid zip archive");

            var normalizedBranch = string.IsNullOrWhiteSpace(branch) ? null : branch.Trim();
            var isMasterBranch = string.Equals(normalizedBranch, "master", StringComparison.OrdinalIgnoreCase);
            var resource = new EditorResource
            {
                LocalFileName = safeFileName,
                ZipContent = content,
                LocalCreateAt = uploadedAt,
                VersionInfo = new VersionInfo
                {
                    Time = uploadedAt,
                    Branch = normalizedBranch,
                    Version = version,
                    FileSize = content.Length,
                },
            };

            if (!editorService.UpdateEditorResource(isMasterBranch, resource))
                return new(false, $"uploaded version {version} is not newer than the current version");

            return new(true);
        }
    }
}
