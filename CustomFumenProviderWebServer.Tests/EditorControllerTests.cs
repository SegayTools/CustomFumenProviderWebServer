using System.IO.Compression;
using CustomFumenProviderWebServer.Controllers;
using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Services.Editor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomFumenProviderWebServer.Tests;

public class EditorControllerTests
{
    [Fact]
    public async Task UpdateEditorZip_WithValidPasswordAndName_UpdatesResource()
    {
        await WithAdminPassword("test", async () =>
        {
            var editorService = new EditorService();
            var controller = CreateController(editorService);
            var content = CreateZip();
            var file = CreateFormFile(content, "OngekiFumenEditor_1.2.3_windows.zip");

            var actionResult = await controller.UpdateEditorZip(file, "test", "master");

            Assert.NotNull(actionResult.Value);
            Assert.True(actionResult.Value.IsSuccess);
            Assert.True(editorService.GetEditorResource(true).IsAvaliable);
            Assert.Equal(content.Length, editorService.GetEditorResource(true).ZipContent.Length);
        });
    }

    [Fact]
    public async Task UpdateEditorZip_WithWrongPassword_Returns403()
    {
        await WithAdminPassword("test", async () =>
        {
            var controller = CreateController(new EditorService());
            var file = CreateFormFile(CreateZip(), "OngekiFumenEditor_1.2.3_windows.zip");

            var actionResult = await controller.UpdateEditorZip(file, "wrong", "master");

            var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
            Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        });
    }

    [Fact]
    public async Task UpdateEditorZip_WithInvalidName_Returns400()
    {
        await WithAdminPassword("test", async () =>
        {
            var controller = CreateController(new EditorService());
            var file = CreateFormFile([1], "editor.zip");

            var actionResult = await controller.UpdateEditorZip(file, "test", "master");

            var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            var result = Assert.IsType<Result>(badRequest.Value);
            Assert.False(result.IsSuccess);
        });
    }

    private static EditorController CreateController(EditorService editorService)
    {
        return new EditorController(
            editorService,
            new EditorUploadService(editorService),
            new AdminAuthService(NullLogger<AdminAuthService>.Instance));
    }

    private static FormFile CreateFormFile(byte[] content, string fileName)
    {
        return new FormFile(new MemoryStream(content), 0, content.Length, "zipFormFile", fileName);
    }

    private static async Task WithAdminPassword(string password, Func<Task> action)
    {
        var previous = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD", password);

        try
        {
            await action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ADMIN_PASSWORD", previous);
        }
    }

    private static byte[] CreateZip()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var entry = zip.CreateEntry("test.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("test");
        }

        return ms.ToArray();
    }
}
