using LakeMichiganFishingAgent.Api.Models;
using LakeMichiganFishingAgent.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

builder.Services.AddHttpClient<NwsMarineForecastProvider>();
builder.Services.AddHttpClient<IZipCodeGeocoder, ZipCodeGeocoder>();
builder.Services.AddSingleton<MockMarineForecastProvider>();
builder.Services.AddSingleton<TripReadinessScorer>();
builder.Services.AddScoped<IMarineForecastProvider>(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var useMock = configuration.GetValue("Noaa:UseMock", true);

    if (useMock)
    {
        return services.GetRequiredService<MockMarineForecastProvider>();
    }

    return services.GetRequiredService<NwsMarineForecastProvider>();
});

var app = builder.Build();

app.UseCors();

app.MapGet("/api/forecast/trip-readiness", async (
    IMarineForecastProvider provider,
    TripReadinessScorer scorer,
    double? lat,
    double? lon,
    string? zip,
    CancellationToken cancellationToken) =>
{
    if (lat.HasValue != lon.HasValue)
    {
        return Results.BadRequest(new { error = "Latitude and longitude must be supplied together." });
    }

    if (lat is < -90 or > 90 || lon is < -180 or > 180)
    {
        return Results.BadRequest(new { error = "Latitude or longitude is outside the valid range." });
    }

    var normalizedZip = NormalizeZipCode(zip);
    if (!string.IsNullOrWhiteSpace(zip) && normalizedZip is null)
    {
        return Results.BadRequest(new { error = "ZIP code must include exactly 5 digits, such as 53202." });
    }

    var request = new ForecastRequest(lat, lon, zip);
    MarineForecast forecast;

    try
    {
        forecast = await provider.GetForecastAsync(request, cancellationToken);
    }
    catch (LocationResolutionException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }

    var scoringPeriods = forecast.MarineProducts.Count > 0
        ? forecast.MarineProducts.SelectMany(product => product.Periods.Take(2)).ToArray()
        : forecast.Periods.Take(2).ToArray();
    var score = scorer.Score(scoringPeriods);

    return Results.Ok(new TripReadinessResponse(
        forecast.Location,
        forecast.Zone,
        forecast.IssuedAt,
        forecast.LastUpdated,
        forecast.Source,
        score,
        forecast.Periods,
        forecast.MarineProducts));
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static string? NormalizeZipCode(string? zip)
{
    if (string.IsNullOrWhiteSpace(zip))
    {
        return null;
    }

    var digits = new string(zip.Where(char.IsDigit).ToArray());
    return digits.Length == 5 ? digits : null;
}

public partial class Program;
