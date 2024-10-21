using CustomFumenProviderWebServer.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using static CustomFumenProviderWebServer.Services.Editor.EditorService;

namespace CustomFumenProviderWebServer.Services.Editor
{
    public partial class EditorService : IEditorService
    {
        private EditorResource latestEditorResource = new();
        private EditorResource masterEditorResource = new();

        public EditorResource GetEditorResource(bool requireMasterBranch)
        {
            return requireMasterBranch ? masterEditorResource : latestEditorResource;
        }
    }
}
