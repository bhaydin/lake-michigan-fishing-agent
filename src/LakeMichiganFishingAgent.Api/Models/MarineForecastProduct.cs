namespace LakeMichiganFishingAgent.Api.Models;

public sealed record MarineForecastProduct(
    string Kind,
    string ProductCode,
    string ProductName,
    string IssuingOffice,
    string Zone,
    DateTimeOffset IssuedAt,
    string Source,
    string Text,
    IReadOnlyList<ForecastPeriod> Periods);
