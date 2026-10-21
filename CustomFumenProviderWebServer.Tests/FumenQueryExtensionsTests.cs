using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Utils;

namespace CustomFumenProviderWebServer.Tests;

public class FumenQueryExtensionsTests
{
    // musicId 1: Basic 5 / Master 15
    // musicId 2: Basic 10 / Master 12
    // musicId 3: Basic 9
    // musicId 4: Basic 13
    private static readonly FumenSet[] Sets =
    [
        CreateSet(1, 5, 15),
        CreateSet(2, 10, 12),
        CreateSet(3, 9),
        CreateSet(4, 13),
    ];

    private static FumenSet CreateSet(int musicId, params float[] levels)
    {
        var set = new FumenSet
        {
            MusicId = musicId,
            Title = $"Title{musicId}",
            Artist = "Artist",
        };

        for (var i = 0; i < levels.Length; i++)
        {
            set.FumenDifficults.Add(new FumenDifficult
            {
                MusicId = musicId,
                DifficultIndex = i,
                Level = levels[i],
            });
        }

        return set;
    }

    private static int[] Apply(float? min, float? max)
    {
        return Sets.AsQueryable()
            .ApplyLevelFilter(min, max)
            .Select(x => x.MusicId)
            .OrderBy(x => x)
            .ToArray();
    }

    [Fact]
    public void NoFilter_ReturnsAllSets()
    {
        Assert.Equal([1, 2, 3, 4], Apply(null, null));
    }

    [Fact]
    public void MinOnly_ReturnsSetsWithAnyDifficultyAtLeastMin()
    {
        Assert.Equal([1, 2, 4], Apply(10, null));
    }

    [Fact]
    public void MaxOnly_ReturnsSetsWithAnyDifficultyAtMostMax()
    {
        Assert.Equal([1, 2, 3], Apply(null, 12));
    }

    [Fact]
    public void MinAndMax_ReturnsOnlySetsWithOneDifficultyInsideRange()
    {
        // 旧实现是两个独立 Any，会错误地包含 1(5/15) 这种上下界由不同难度满足的谱面。
        Assert.Equal([2], Apply(10, 12));
    }

    [Fact]
    public void MinAndMax_IsInclusive()
    {
        Assert.Equal([2], Apply(10, 12));
        Assert.Equal([2], Apply(10, 10));
        Assert.Equal([2], Apply(12, 12));
    }

    [Fact]
    public void MinGreaterThanMax_ReturnsEmpty()
    {
        Assert.Empty(Apply(13, 9));
    }
}
