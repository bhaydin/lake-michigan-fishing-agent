namespace LakeMichiganFishingAgent.Api.Models;

public sealed record MarineForecast(
    string Location,
    string Zone,
    DateTimeOffset IssuedAt,
    DateTimeOffset LastUpdated,
    string Source,
    IReadOnlyList<ForecastPeriod> Periods,
    IReadOnlyList<MarineForecastProduct> MarineProducts);
