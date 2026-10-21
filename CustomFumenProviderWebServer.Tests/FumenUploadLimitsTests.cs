using CustomFumenProviderWebServer.Utils;
using Microsoft.AspNetCore.Http.Features;

namespace CustomFumenProviderWebServer.Tests;

public class FumenUploadLimitsTests
{
    [Fact]
    public void MaxRequestBodySize_Is300MB()
    {
        Assert.Equal(300_000_000, FumenUploadLimits.MaxRequestBodySize);
    }

    [Fact]
    public void ConfigureFormOptions_RaisesMultipartLimitAboveDefault()
    {
        var options = new FormOptions();
        var defaultLimit = options.MultipartBodyLengthLimit;

        FumenUploadLimits.ConfigureFormOptions(options);

        Assert.True(options.MultipartBodyLengthLimit > defaultLimit);
        Assert.Equal(FumenUploadLimits.MaxRequestBodySize, options.MultipartBodyLengthLimit);
    }
}
