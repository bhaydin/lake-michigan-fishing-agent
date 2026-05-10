using System.Net;
using System.Net.Http.Json;
using LakeMichiganFishingAgent.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace LakeMichiganFishingAgent.Tests;

public sealed class ApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiContractTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Noaa:UseMock", "true");
        });
    }

    [Fact]
    public async Task TripReadinessEndpoint_ReturnsDocumentedContract()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/forecast/trip-readiness");
        var contract = await response.Content.ReadFromJsonAsync<TripReadinessResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(contract);
        Assert.False(string.IsNullOrWhiteSpace(contract!.Location));
        Assert.False(string.IsNullOrWhiteSpace(contract.Zone));
        Assert.False(string.IsNullOrWhiteSpace(contract.Source));
        Assert.NotEmpty(contract.Periods);
        Assert.Contains(contract.Readiness.Rating, new[] { "Good", "Caution", "Bad" });
        Assert.NotEmpty(contract.Readiness.Rules);
    }

    [Fact]
    public async Task TripReadinessEndpoint_AcceptsZipCodeLocation()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/forecast/trip-readiness?zip=53202");
        var contract = await response.Content.ReadFromJsonAsync<TripReadinessResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(contract);
        Assert.Contains("53202", contract!.Location);
        Assert.NotEmpty(contract.Periods);
    }
}
