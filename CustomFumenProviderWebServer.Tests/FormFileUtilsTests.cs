using CustomFumenProviderWebServer.Utils;
using Microsoft.AspNetCore.Http;

namespace CustomFumenProviderWebServer.Tests;

public class FormFileUtilsTests
{
    [Fact]
    public async Task SaveAsync_CopiesContentAndDisposesUploadStream()
    {
        using var temp = new TempFolderScope();
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var trackingStream = new TrackingStream(payload);
        var formFile = new FakeFormFile(trackingStream, "audio.wav");
        var savePath = Path.Combine(temp.Path, "saved.bin");

        await FormFileUtils.SaveAsync(formFile, savePath);

        Assert.Equal(payload, await File.ReadAllBytesAsync(savePath));
        Assert.True(trackingStream.Disposed);
    }

    [Fact]
    public async Task SaveAsync_NullFile_DoesNotCreateFile()
    {
        using var temp = new TempFolderScope();
        var savePath = Path.Combine(temp.Path, "saved.bin");

        await FormFileUtils.SaveAsync(null!, savePath);

        Assert.False(File.Exists(savePath));
    }

    private sealed class TrackingStream : MemoryStream
    {
        public TrackingStream(byte[] buffer) : base(buffer)
        {
        }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class FakeFormFile : IFormFile
    {
        private readonly Stream stream;

        public FakeFormFile(Stream stream, string fileName)
        {
            this.stream = stream;
            FileName = fileName;
        }

        public string ContentType => "application/octet-stream";
        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length => stream.Length;
        public string Name => "file";
        public string FileName { get; }

        public void CopyTo(Stream target)
        {
            stream.CopyTo(target);
        }

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            return stream.CopyToAsync(target, cancellationToken);
        }

        public Stream OpenReadStream()
        {
            return stream;
        }
    }
}
