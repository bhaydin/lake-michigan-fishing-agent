namespace LakeMichiganFishingAgent.Api.Models;

public sealed record ReadinessScore(
    string Rating,
    int Score,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Rules);
