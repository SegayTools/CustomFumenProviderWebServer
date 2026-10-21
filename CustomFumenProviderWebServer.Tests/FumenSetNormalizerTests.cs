using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Utils;

namespace CustomFumenProviderWebServer.Tests;

public class FumenSetNormalizerTests
{
    [Fact]
    public void ApplyMusicId_UpdatesParentAndAllDifficulties()
    {
        var set = new FumenSet { MusicId = 100 };
        set.FumenDifficults.Add(new FumenDifficult { MusicId = 100, DifficultIndex = 0 });
        set.FumenDifficults.Add(new FumenDifficult { MusicId = 100, DifficultIndex = 1 });

        FumenSetNormalizer.ApplyMusicId(set, 30001);

        Assert.Equal(30001, set.MusicId);
        Assert.All(set.FumenDifficults, difficult => Assert.Equal(30001, difficult.MusicId));
    }

    [Fact]
    public void ApplyMusicId_WithoutDifficulties_UpdatesParent()
    {
        var set = new FumenSet { MusicId = 100 };

        FumenSetNormalizer.ApplyMusicId(set, 30002);

        Assert.Equal(30002, set.MusicId);
        Assert.Empty(set.FumenDifficults);
    }
}
