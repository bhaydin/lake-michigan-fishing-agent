namespace LakeMichiganFishingAgent.Api.Models;

public sealed record GeoPoint(
    double Latitude,
    double Longitude,
    string Label);
