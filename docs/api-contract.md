# API Contract

## `GET /api/forecast/trip-readiness`

Returns the normalized marine forecast and the trip-readiness score used by the frontend.

Optional query parameters:

- `lat` and `lon`: browser-provided coordinates. When present, the API resolves the correct NWS forecast endpoint from `https://api.weather.gov/points/{lat},{lon}`.
- `zip`: US ZIP code. The API geocodes the ZIP code, then resolves the NWS forecast endpoint from the coordinates.

When `Noaa__UseMock=true`, the API always returns mock data. When `Noaa__UseMock=false`, live NWS data is attempted and the mock provider is used as a fallback if geocoding or forecast retrieval fails.

```json
{
  "location": "Lake Michigan near Milwaukee",
  "zone": "LMZ644",
  "issuedAt": "2026-05-09T22:00:00Z",
  "lastUpdated": "2026-05-09T22:20:00Z",
  "source": "Mock NOAA/NWS marine forecast",
  "readiness": {
    "rating": "Caution",
    "score": 60,
    "reasons": [
      "Waves reach 2.5 ft, which calls for caution."
    ],
    "rules": [
      "Good: waves below 2 ft, wind below 15 mph, and no hazards in the next two periods.",
      "Caution: waves from 2 to 3.5 ft, wind from 15 to 20 mph, or non-severe advisory language.",
      "Bad: waves above 3.5 ft, wind above 20 mph, or hazards mentioning small craft, gale, thunder, or storms."
    ]
  },
  "periods": [
    {
      "name": "Today",
      "startsAt": "2026-05-09T22:00:00Z",
      "endsAt": "2026-05-10T10:00:00Z",
      "windSpeedMph": 11,
      "windDirection": "NW",
      "waveHeightFeet": 1.5,
      "weatherSummary": "Partly sunny with light chop building late",
      "hazards": []
    }
  ]
}
```

## Scoring Rules

- `Good`: next two forecast periods remain below 2 ft waves, below 15 mph wind, and include no hazards.
- `Caution`: next two forecast periods include waves from 2 to 3.5 ft, winds from 15 to 20 mph, or non-severe advisory language.
- `Bad`: next two forecast periods include waves above 3.5 ft, winds above 20 mph, or hazards mentioning small craft, gale, thunder, or storms.
