namespace LakeMichiganFishingAgent.Api.Models;

public sealed record TripReadinessResponse(
    string Location,
    string Zone,
    DateTimeOffset IssuedAt,
    DateTimeOffset LastUpdated,
    string Source,
    ReadinessScore Readiness,
    IReadOnlyList<ForecastPeriod> Periods,
    IReadOnlyList<MarineForecastProduct> MarineProducts);
