using LakeMichiganFishingAgent.Api.Models;

namespace LakeMichiganFishingAgent.Api.Services;

public interface IMarineForecastProvider
{
    Task<MarineForecast> GetForecastAsync(ForecastRequest request, CancellationToken cancellationToken);
}
