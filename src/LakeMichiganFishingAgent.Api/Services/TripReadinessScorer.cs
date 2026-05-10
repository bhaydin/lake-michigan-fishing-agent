using LakeMichiganFishingAgent.Api.Models;

namespace LakeMichiganFishingAgent.Api.Services;

public sealed class TripReadinessScorer
{
    public static readonly string[] Rules =
    [
        "Good: assessed nearshore/open-water periods stay below 2 ft waves, below 15 mph wind, and include no hazards.",
        "Caution: waves from 2 to 3.5 ft, wind from 15 to 20 mph, or non-severe advisory language.",
        "Bad: waves above 3.5 ft, wind above 20 mph, or hazards mentioning small craft, gale, thunder, or storms."
    ];

    public ReadinessScore Score(IReadOnlyList<ForecastPeriod> periods)
    {
        var relevantPeriods = periods.ToArray();
        if (relevantPeriods.Length == 0)
        {
            return new ReadinessScore("Bad", 0, ["No forecast periods are available."], Rules);
        }

        var maxWind = relevantPeriods.Max(period => period.WindSpeedMph);
        var maxWave = relevantPeriods.Max(period => period.WaveHeightFeet);
        var hazards = relevantPeriods.SelectMany(period => period.Hazards).ToArray();
        var reasons = new List<string>();

        if (maxWave > 3.5)
        {
            reasons.Add($"Waves peak at {maxWave:0.#} ft, above the 3.5 ft bad-weather threshold.");
        }

        if (maxWind > 20)
        {
            reasons.Add($"Winds peak at {maxWind} mph, above the 20 mph bad-weather threshold.");
        }

        var severeHazards = hazards
            .Where(hazard => hazard.Contains("small craft", StringComparison.OrdinalIgnoreCase)
                || hazard.Contains("gale", StringComparison.OrdinalIgnoreCase)
                || hazard.Contains("thunder", StringComparison.OrdinalIgnoreCase)
                || hazard.Contains("storm", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (severeHazards.Length > 0)
        {
            reasons.Add($"Hazards mention {string.Join(", ", severeHazards)}.");
        }

        if (reasons.Count > 0)
        {
            return new ReadinessScore("Bad", 25, reasons, Rules);
        }

        if (maxWave >= 2 || maxWind >= 15 || hazards.Length > 0)
        {
            if (maxWave >= 2)
            {
                reasons.Add($"Waves reach {maxWave:0.#} ft, which calls for caution.");
            }

            if (maxWind >= 15)
            {
                reasons.Add($"Winds reach {maxWind} mph, which calls for caution.");
            }

            if (hazards.Length > 0)
            {
                reasons.Add($"Forecast notes {string.Join(", ", hazards)}.");
            }

            return new ReadinessScore("Caution", 60, reasons, Rules);
        }

        return new ReadinessScore(
            "Good",
            90,
            [$"Assessed conditions stay under 2 ft waves and 15 mph wind across {relevantPeriods.Length} forecast period(s)."],
            Rules);
    }
}
