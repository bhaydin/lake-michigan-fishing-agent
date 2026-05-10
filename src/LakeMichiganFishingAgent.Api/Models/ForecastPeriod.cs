namespace LakeMichiganFishingAgent.Api.Models;

public sealed record ForecastPeriod(
    string Name,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int WindSpeedMph,
    string WindDirection,
    double WaveHeightFeet,
    string WeatherSummary,
    IReadOnlyList<string> Hazards);
