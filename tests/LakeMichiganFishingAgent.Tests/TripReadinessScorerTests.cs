using LakeMichiganFishingAgent.Api.Models;
using LakeMichiganFishingAgent.Api.Services;
using Xunit;

namespace LakeMichiganFishingAgent.Tests;

public sealed class TripReadinessScorerTests
{
    private readonly TripReadinessScorer _scorer = new();

    [Fact]
    public void Score_ReturnsGood_WhenWindAndWavesAreCalm()
    {
        var result = _scorer.Score([
            Period(windSpeedMph: 9, waveHeightFeet: 1.2),
            Period(windSpeedMph: 12, waveHeightFeet: 1.8)
        ]);

        Assert.Equal("Good", result.Rating);
        Assert.Equal(90, result.Score);
    }

    [Fact]
    public void Score_ReturnsCaution_WhenWindOrWavesAreMarginal()
    {
        var result = _scorer.Score([
            Period(windSpeedMph: 15, waveHeightFeet: 1.5),
            Period(windSpeedMph: 12, waveHeightFeet: 2.2)
        ]);

        Assert.Equal("Caution", result.Rating);
        Assert.Contains(result.Reasons, reason => reason.Contains("caution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Score_ReturnsBad_WhenSevereThresholdsOrHazardsArePresent()
    {
        var result = _scorer.Score([
            Period(windSpeedMph: 18, waveHeightFeet: 2.5, hazards: ["Small Craft Advisory"]),
            Period(windSpeedMph: 22, waveHeightFeet: 4.0)
        ]);

        Assert.Equal("Bad", result.Rating);
        Assert.Equal(25, result.Score);
        Assert.Contains(result.Reasons, reason => reason.Contains("Small Craft", StringComparison.OrdinalIgnoreCase));
    }

    private static ForecastPeriod Period(
        int windSpeedMph,
        double waveHeightFeet,
        IReadOnlyList<string>? hazards = null) =>
        new(
            "Test",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(12),
            windSpeedMph,
            "NW",
            waveHeightFeet,
            "Test summary",
            hazards ?? []);
}
