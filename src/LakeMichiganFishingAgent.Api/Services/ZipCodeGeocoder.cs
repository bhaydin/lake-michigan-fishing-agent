using System.Globalization;
using System.Text.Json;
using LakeMichiganFishingAgent.Api.Models;

namespace LakeMichiganFishingAgent.Api.Services;

public sealed class ZipCodeGeocoder(HttpClient httpClient) : IZipCodeGeocoder
{
    private static readonly IReadOnlyDictionary<string, GeoPoint> FallbackZipCodes =
        new Dictionary<string, GeoPoint>(StringComparer.OrdinalIgnoreCase)
        {
            ["60601"] = new(41.8853, -87.6216, "Chicago, IL 60601"),
            ["53202"] = new(43.0447, -87.8990, "Milwaukee, WI 53202"),
            ["49417"] = new(43.0631, -86.2284, "Grand Haven, MI 49417"),
            ["46360"] = new(41.7075, -86.8950, "Michigan City, IN 46360"),
            ["49684"] = new(44.7631, -85.6206, "Traverse City, MI 49684")
        };

    public async Task<GeoPoint?> GeocodeAsync(string zipCode, CancellationToken cancellationToken)
    {
        var normalizedZip = NormalizeZipCode(zipCode);
        if (normalizedZip is null)
        {
            return null;
        }

        try
        {
            using var response = await httpClient.GetAsync(
                $"https://api.zippopotam.us/us/{normalizedZip}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var root = document.RootElement;
            var place = root.GetProperty("places")[0];
            var latitude = double.Parse(place.GetProperty("latitude").GetString() ?? "0", CultureInfo.InvariantCulture);
            var longitude = double.Parse(place.GetProperty("longitude").GetString() ?? "0", CultureInfo.InvariantCulture);
            var city = place.GetProperty("place name").GetString() ?? "ZIP code";
            var state = place.GetProperty("state abbreviation").GetString() ?? "US";

            return new GeoPoint(latitude, longitude, $"{city}, {state} {normalizedZip}");
        }
        catch
        {
            return FallbackZipCodes.GetValueOrDefault(normalizedZip);
        }
    }

    private static string? NormalizeZipCode(string zipCode)
    {
        var digits = new string(zipCode.Where(char.IsDigit).Take(5).ToArray());
        return digits.Length == 5 ? digits : null;
    }
}
