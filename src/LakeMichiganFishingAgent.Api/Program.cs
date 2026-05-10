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
    var request = new ForecastRequest(lat, lon, zip);
    var forecast = await provider.GetForecastAsync(request, cancellationToken);
    var score = scorer.Score(forecast.Periods);

    return Results.Ok(new TripReadinessResponse(
        forecast.Location,
        forecast.Zone,
        forecast.IssuedAt,
        forecast.LastUpdated,
        forecast.Source,
        score,
        forecast.Periods));
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
