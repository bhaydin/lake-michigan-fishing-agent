using LakeMichiganFishingAgent.Api.Models;

namespace LakeMichiganFishingAgent.Api.Services;

public interface IZipCodeGeocoder
{
    Task<GeoPoint?> GeocodeAsync(string zipCode, CancellationToken cancellationToken);
}
