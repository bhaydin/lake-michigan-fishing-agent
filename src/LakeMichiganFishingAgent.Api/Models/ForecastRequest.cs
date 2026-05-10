namespace LakeMichiganFishingAgent.Api.Models;

public sealed record ForecastRequest(
    double? Latitude,
    double? Longitude,
    string? ZipCode)
{
    public bool HasCoordinates => Latitude.HasValue && Longitude.HasValue;
    public bool HasZipCode => !string.IsNullOrWhiteSpace(ZipCode);
}
