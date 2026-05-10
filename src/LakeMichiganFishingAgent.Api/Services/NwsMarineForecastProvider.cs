using System.Globalization;
using System.Text.Json;
using LakeMichiganFishingAgent.Api.Models;

namespace LakeMichiganFishingAgent.Api.Services;

public sealed class NwsMarineForecastProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    MockMarineForecastProvider fallback,
    IZipCodeGeocoder zipCodeGeocoder,
    ILogger<NwsMarineForecastProvider> logger) : IMarineForecastProvider
{
    public async Task<MarineForecast> GetForecastAsync(ForecastRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var locationPoint = await ResolveLocationAsync(request, cancellationToken);
            var resolvedForecast = await ResolveForecastEndpointAsync(locationPoint, cancellationToken);
            var endpoint = resolvedForecast?.Endpoint ?? configuration["Noaa:ForecastUrl"];

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return await fallback.GetForecastAsync(request, cancellationToken);
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
            httpRequest.Headers.UserAgent.ParseAdd("lake-michigan-fishing-agent/0.1 (+https://example.com/lake-michigan-fishing-agent)");
            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var root = document.RootElement;
            var properties = root.GetProperty("properties");
            var generatedAt = properties.TryGetProperty("generatedAt", out var generatedAtElement)
                ? ReadDateTimeOffset(generatedAtElement.GetString(), DateTimeOffset.UtcNow)
                : DateTimeOffset.UtcNow;

            var periods = properties.GetProperty("periods")
                .EnumerateArray()
                .Take(6)
                .Select(ParsePeriod)
                .ToArray();

            return new MarineForecast(
                locationPoint?.Label ?? configuration["Noaa:Location"] ?? "Lake Michigan",
                resolvedForecast?.Zone ?? configuration["Noaa:Zone"] ?? "NWS point forecast",
                generatedAt,
                DateTimeOffset.UtcNow,
                endpoint,
                periods);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Live NWS forecast lookup failed; falling back to mock forecast data.");
            return await fallback.GetForecastAsync(request, cancellationToken);
        }
    }

    private async Task<GeoPoint?> ResolveLocationAsync(ForecastRequest request, CancellationToken cancellationToken)
    {
        if (request.HasCoordinates)
        {
            return new GeoPoint(
                request.Latitude!.Value,
                request.Longitude!.Value,
                $"Browser location {request.Latitude:0.0000}, {request.Longitude:0.0000}");
        }

        if (request.HasZipCode)
        {
            return await zipCodeGeocoder.GeocodeAsync(request.ZipCode!, cancellationToken);
        }

        return null;
    }

    private async Task<NwsResolvedForecast?> ResolveForecastEndpointAsync(GeoPoint? point, CancellationToken cancellationToken)
    {
        if (point is null)
        {
            return null;
        }

        var latitude = point.Latitude.ToString("0.####", CultureInfo.InvariantCulture);
        var longitude = point.Longitude.ToString("0.####", CultureInfo.InvariantCulture);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.weather.gov/points/{latitude},{longitude}");

        request.Headers.UserAgent.ParseAdd("lake-michigan-fishing-agent/0.1 (+https://example.com/lake-michigan-fishing-agent)");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var properties = document.RootElement.GetProperty("properties");
        var endpoint = properties.GetProperty("forecast").GetString();
        var zone = properties.TryGetProperty("forecastZone", out var zoneElement)
            ? zoneElement.GetString()?.Split('/').LastOrDefault()
            : null;

        return string.IsNullOrWhiteSpace(endpoint)
            ? null
            : new NwsResolvedForecast(endpoint, zone ?? "NWS point forecast");
    }

    private static ForecastPeriod ParsePeriod(JsonElement period)
    {
        var name = ReadString(period, "name", "Forecast period");
        var startsAt = ReadDateTimeOffset(ReadString(period, "startTime", ""), DateTimeOffset.UtcNow);
        var endsAt = ReadDateTimeOffset(ReadString(period, "endTime", ""), startsAt.AddHours(12));
        var windSpeed = ParseWindSpeed(ReadString(period, "windSpeed", "0 mph"));
        var windDirection = ReadString(period, "windDirection", "Variable");
        var summary = ReadString(period, "detailedForecast", ReadString(period, "shortForecast", "No summary available"));
        var waveHeight = ParseWaveHeight(summary);
        var hazards = ExtractHazards(summary);

        return new ForecastPeriod(name, startsAt, endsAt, windSpeed, windDirection, waveHeight, summary, hazards);
    }

    private static string ReadString(JsonElement element, string propertyName, string fallback) =>
        element.TryGetProperty(propertyName, out var value) ? value.GetString() ?? fallback : fallback;

    private static DateTimeOffset ReadDateTimeOffset(string? value, DateTimeOffset fallback) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : fallback;

    private static int ParseWindSpeed(string value)
    {
        var numbers = value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => int.TryParse(token, out var number) ? number : (int?)null)
            .Where(number => number.HasValue)
            .Select(number => number!.Value)
            .ToArray();

        return numbers.Length == 0 ? 0 : (int)Math.Round(numbers.Average());
    }

    private static double ParseWaveHeight(string summary)
    {
        var words = summary.Replace(".", " ", StringComparison.Ordinal)
            .Replace(",", " ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < words.Length - 1; index++)
        {
            if (double.TryParse(words[index], out var number)
                && words[index + 1].StartsWith("ft", StringComparison.OrdinalIgnoreCase))
            {
                return number;
            }
        }

        return 0;
    }

    private static IReadOnlyList<string> ExtractHazards(string summary)
    {
        var hazards = new List<string>();
        if (summary.Contains("small craft", StringComparison.OrdinalIgnoreCase))
        {
            hazards.Add("Small Craft Advisory mentioned");
        }

        if (summary.Contains("gale", StringComparison.OrdinalIgnoreCase))
        {
            hazards.Add("Gale conditions mentioned");
        }

        if (summary.Contains("thunder", StringComparison.OrdinalIgnoreCase))
        {
            hazards.Add("Thunderstorm risk mentioned");
        }

        return hazards;
    }

    private sealed record NwsResolvedForecast(string Endpoint, string Zone);
}
