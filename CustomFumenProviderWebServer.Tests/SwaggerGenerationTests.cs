using CustomFumenProviderWebServer.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;

namespace CustomFumenProviderWebServer.Tests;

public class SwaggerGenerationTests
{
    [Fact]
    public void SwaggerDocument_CanBeGenerated_WithEditorUploadEndpoint()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var environment = new TestWebHostEnvironment();
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddSingleton<IWebHostEnvironment>(environment);
        services.AddControllers().AddApplicationPart(typeof(EditorController).Assembly);
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        using var provider = services.BuildServiceProvider();
        var swagger = provider.GetRequiredService<ISwaggerProvider>();

        var document = swagger.GetSwagger("v1");

        Assert.True(document.Paths.ContainsKey("/editor/updateEditorZip"));
        var operation = document.Paths["/editor/updateEditorZip"].Operations[OperationType.Put];
        Assert.NotNull(operation);
        Assert.NotNull(operation.RequestBody);
        Assert.True(operation.RequestBody.Content.ContainsKey("multipart/form-data"));
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "CustomFumenProviderWebServer.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
