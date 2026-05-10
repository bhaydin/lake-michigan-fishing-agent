using LakeMichiganFishingAgent.Api.Models;

namespace LakeMichiganFishingAgent.Api.Services;

public sealed class MockMarineForecastProvider(IConfiguration configuration) : IMarineForecastProvider
{
    public Task<MarineForecast> GetForecastAsync(ForecastRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var location = ResolveLocationLabel(request);
        var zone = configuration["Noaa:Zone"] ?? "LMZ644";

        IReadOnlyList<ForecastPeriod> periods =
        [
            new(
                "Today",
                now,
                now.AddHours(12),
                11,
                "NW",
                1.5,
                "Partly sunny with light chop building late",
                []),
            new(
                "Tonight",
                now.AddHours(12),
                now.AddHours(24),
                17,
                "N",
                2.5,
                "Mostly cloudy with scattered showers",
                ["Small craft should monitor conditions"]),
            new(
                "Tomorrow",
                now.AddHours(24),
                now.AddHours(36),
                23,
                "NE",
                4.0,
                "Windy with waves increasing",
                ["Small Craft Advisory possible"])
        ];

        var nearshore = new MarineForecastProduct(
            "Nearshore",
            "NSH",
            "Nearshore Marine Forecast",
            "KMKX",
            configuration["Noaa:NearshoreZone"] ?? zone,
            now,
            "Mock NOAA/NWS nearshore marine forecast",
            "Mock nearshore forecast for waters within five nautical miles of shore.",
            periods.Take(2).ToArray());

        var openWater = new MarineForecastProduct(
            "Open Water",
            "GLF",
            "Great Lakes Forecast",
            "KMKX",
            configuration["Noaa:OpenWaterZone"] ?? "LMZ671",
            now,
            "Mock NOAA/NWS open lake forecast",
            "Mock open lake forecast for waters beyond five nautical miles of shore.",
            periods.Skip(1).ToArray());

        return Task.FromResult(new MarineForecast(
            location,
            zone,
            now.AddMinutes(-20),
            now,
            "Mock NOAA/NWS marine forecast",
            periods,
            [nearshore, openWater]));
    }

    private string ResolveLocationLabel(ForecastRequest request)
    {
        if (request.HasZipCode)
        {
            return $"Forecast near ZIP {request.ZipCode}";
        }

        if (request.HasCoordinates)
        {
            return $"Forecast near {request.Latitude:0.0000}, {request.Longitude:0.0000}";
        }

        return configuration["Noaa:Location"] ?? "Lake Michigan near Milwaukee";
    }
}
