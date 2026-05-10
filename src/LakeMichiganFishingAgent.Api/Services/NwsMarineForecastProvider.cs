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

            using var httpRequest = CreateNoaaRequest(endpoint);
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
            var marineProducts = await GetMarineProductsAsync(resolvedForecast?.Office, cancellationToken);

            return new MarineForecast(
                locationPoint?.Label ?? configuration["Noaa:Location"] ?? "Lake Michigan",
                resolvedForecast?.Zone ?? configuration["Noaa:Zone"] ?? "NWS point forecast",
                generatedAt,
                DateTimeOffset.UtcNow,
                endpoint,
                periods,
                marineProducts);
        }
        catch (LocationResolutionException)
        {
            throw;
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
            var point = await zipCodeGeocoder.GeocodeAsync(request.ZipCode!, cancellationToken);
            return point ?? throw new LocationResolutionException($"ZIP code {request.ZipCode} could not be geocoded.");
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
        using var request = CreateNoaaRequest($"https://api.weather.gov/points/{latitude},{longitude}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var properties = document.RootElement.GetProperty("properties");
        var endpoint = properties.GetProperty("forecast").GetString();
        var office = properties.TryGetProperty("cwa", out var officeElement)
            ? officeElement.GetString()
            : null;
        var zone = properties.TryGetProperty("forecastZone", out var zoneElement)
            ? zoneElement.GetString()?.Split('/').LastOrDefault()
            : null;

        return string.IsNullOrWhiteSpace(endpoint)
            ? null
            : new NwsResolvedForecast(endpoint, zone ?? "NWS point forecast", office ?? configuration["Noaa:Office"] ?? "MKX");
    }

    private async Task<IReadOnlyList<MarineForecastProduct>> GetMarineProductsAsync(string? office, CancellationToken cancellationToken)
    {
        var marineOffice = (configuration["Noaa:MarineOffice"] ?? office ?? "MKX").TrimStart('K');
        var issuingOffice = $"K{marineOffice}";
        var nearshoreZone = configuration["Noaa:NearshoreZone"] ?? configuration["Noaa:Zone"] ?? "LMZ644";
        var openWaterZone = configuration["Noaa:OpenWaterZone"] ?? "LMZ671";
        var products = new List<MarineForecastProduct>();

        var nearshore = await GetLatestProductAsync("NSH", marineOffice, issuingOffice, nearshoreZone, "Nearshore", cancellationToken);
        if (nearshore is not null)
        {
            products.Add(nearshore);
        }

        var openWater = await GetLatestProductAsync("GLF", null, issuingOffice, openWaterZone, "Open Water", cancellationToken);
        if (openWater is not null)
        {
            products.Add(openWater);
        }

        return products;
    }

    private async Task<MarineForecastProduct?> GetLatestProductAsync(
        string productCode,
        string? location,
        string issuingOffice,
        string zone,
        string kind,
        CancellationToken cancellationToken)
    {
        var listUrl = location is null
            ? $"https://api.weather.gov/products/types/{productCode}"
            : $"https://api.weather.gov/products/types/{productCode}/locations/{location}";

        using var listRequest = CreateNoaaRequest(listUrl);
        using var listResponse = await httpClient.SendAsync(listRequest, cancellationToken);
        listResponse.EnsureSuccessStatusCode();

        await using var listStream = await listResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var listDocument = await JsonDocument.ParseAsync(listStream, cancellationToken: cancellationToken);

        var latestProduct = listDocument.RootElement.GetProperty("@graph")
            .EnumerateArray()
            .Where(product => ReadString(product, "issuingOffice", "").Equals(issuingOffice, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(product => ReadDateTimeOffset(ReadString(product, "issuanceTime", ""), DateTimeOffset.MinValue))
            .FirstOrDefault();

        if (latestProduct.ValueKind is JsonValueKind.Undefined)
        {
            return null;
        }

        var productUrl = latestProduct.GetProperty("@id").GetString();
        if (string.IsNullOrWhiteSpace(productUrl))
        {
            return null;
        }

        using var productRequest = CreateNoaaRequest(productUrl);
        using var productResponse = await httpClient.SendAsync(productRequest, cancellationToken);
        productResponse.EnsureSuccessStatusCode();

        await using var productStream = await productResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var productDocument = await JsonDocument.ParseAsync(productStream, cancellationToken: cancellationToken);
        var product = productDocument.RootElement;
        var text = ReadString(product, "productText", "");
        var zoneText = ExtractZoneText(text, zone);
        var assessmentText = string.IsNullOrWhiteSpace(zoneText) ? text : zoneText;

        return new MarineForecastProduct(
            kind,
            productCode,
            ReadString(product, "productName", productCode),
            ReadString(product, "issuingOffice", issuingOffice),
            zone,
            ReadDateTimeOffset(ReadString(product, "issuanceTime", ""), DateTimeOffset.UtcNow),
            productUrl,
            assessmentText,
            ParseMarineProductPeriods(assessmentText));
    }

    private static HttpRequestMessage CreateNoaaRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("lake-michigan-fishing-agent/0.1 (+https://example.com/lake-michigan-fishing-agent)");
        return request;
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

    private static string ExtractZoneText(string productText, string zone)
    {
        var zoneNumber = new string(zone.Where(char.IsDigit).ToArray());
        if (zoneNumber.Length == 0)
        {
            return "";
        }

        var section = productText
            .Split("$$", StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .FirstOrDefault(value => System.Text.RegularExpressions.Regex.IsMatch(
                value,
                $@"(?i)(?:LMZ)?{System.Text.RegularExpressions.Regex.Escape(zoneNumber)}(?:\D|$)"));

        return section ?? "";
    }

    private static IReadOnlyList<ForecastPeriod> ParseMarineProductPeriods(string productText)
    {
        var normalized = productText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var periodBlocks = new List<(string Name, string Text)>();
        var matches = System.Text.RegularExpressions.Regex.Matches(
            normalized,
            @"(?ms)^\.(?<name>[A-Z0-9 /]+?)\.\.\.(?<text>.*?)(?=^\.[A-Z0-9 /]+?\.\.\.|\$\$|\z)");

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var name = ToTitleCase(match.Groups["name"].Value.Trim());
            if (name.Equals("Synopsis", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = CollapseWhitespace(match.Groups["text"].Value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                periodBlocks.Add((name, text));
            }
        }

        var now = DateTimeOffset.UtcNow;
        return periodBlocks.Take(8)
            .Select((block, index) => new ForecastPeriod(
                block.Name,
                now.AddHours(index * 12),
                now.AddHours((index + 1) * 12),
                ParseWindSpeed(block.Text),
                ParseWindDirection(block.Text),
                ParseWaveHeight(block.Text),
                block.Text,
                ExtractHazards(block.Text)))
            .ToArray();
    }

    private static string ReadString(JsonElement element, string propertyName, string fallback) =>
        element.TryGetProperty(propertyName, out var value) ? value.GetString() ?? fallback : fallback;

    private static DateTimeOffset ReadDateTimeOffset(string? value, DateTimeOffset fallback) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : fallback;

    private static int ParseWindSpeed(string value)
    {
        var windText = System.Text.RegularExpressions.Regex
            .Matches(value, @"(?i)(?:wind|winds)[^.]*?(?:mph|kt|knots)")
            .Select(match => match.Value)
            .DefaultIfEmpty(value)
            .First();

        var numbers = System.Text.RegularExpressions.Regex
            .Matches(windText, @"(?i)(\d+)(?=(?:\s+to\s+\d+)?\s*(?:mph|kt|knots)|\s+to\s+\d+)")
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();

        var rangeHighs = System.Text.RegularExpressions.Regex
            .Matches(windText, @"(?i)\d+\s+to\s+(\d+)\s*(?:mph|kt|knots)?")
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));

        numbers = numbers.Concat(rangeHighs).ToArray();

        return numbers.Length == 0 ? 0 : numbers.Max();
    }

    private static double ParseWaveHeight(string summary)
    {
        var waveText = System.Text.RegularExpressions.Regex
            .Matches(summary, @"(?i)waves?[^.]*?(?:ft|foot|feet)")
            .Select(match => match.Value)
            .DefaultIfEmpty(summary)
            .First();

        var values = new List<double>();
        var words = waveText.Replace(".", " ", StringComparison.Ordinal)
            .Replace(",", " ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < words.Length - 1; index++)
        {
            if (double.TryParse(words[index], out var number))
            {
                if (index + 2 < words.Length
                    && words[index + 1].Equals("to", StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(words[index + 2], out var rangeHigh)
                    && index + 3 < words.Length
                    && words[index + 3].StartsWith("ft", StringComparison.OrdinalIgnoreCase))
                {
                    values.Add(rangeHigh);
                    continue;
                }

                if (words[index + 1].StartsWith("ft", StringComparison.OrdinalIgnoreCase)
                    || words[index + 1].StartsWith("foot", StringComparison.OrdinalIgnoreCase))
                {
                    values.Add(number);
                }
            }
        }

        return values.Count == 0 ? 0 : values.Max();
    }

    private static string ParseWindDirection(string summary)
    {
        var directions = new[]
        {
            "northwest", "northeast", "southwest", "southeast", "north", "south", "east", "west"
        };

        var direction = directions.FirstOrDefault(value => summary.Contains(value, StringComparison.OrdinalIgnoreCase));
        return direction is null ? "Variable" : ToCompass(direction);
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

        if (summary.Contains("rain shower", StringComparison.OrdinalIgnoreCase))
        {
            hazards.Add("Rain showers mentioned");
        }

        return hazards;
    }

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string ToTitleCase(string value) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());

    private static string ToCompass(string value) =>
        value.ToLowerInvariant() switch
        {
            "north" => "N",
            "south" => "S",
            "east" => "E",
            "west" => "W",
            "northeast" => "NE",
            "northwest" => "NW",
            "southeast" => "SE",
            "southwest" => "SW",
            _ => "Variable"
        };

    private sealed record NwsResolvedForecast(string Endpoint, string Zone, string Office);
}
