# API Contract

## `GET /api/forecast/trip-readiness`

Returns the normalized marine forecast and the trip-readiness score used by the frontend.

Optional query parameters:

- `lat` and `lon`: browser-provided coordinates. When present, the API resolves the correct NWS forecast endpoint from `https://api.weather.gov/points/{lat},{lon}`.
- `zip`: US ZIP code. The API geocodes the ZIP code, then resolves the NWS forecast endpoint from the coordinates.

When `Noaa__UseMock=true`, the API always returns mock data. When `Noaa__UseMock=false`, live NWS data is attempted and the mock provider is used as a fallback if geocoding or forecast retrieval fails.

For Lake Michigan readiness, the API also pulls NOAA text marine products:

- `NSH`: nearshore marine forecast, configured by `Noaa__NearshoreZone`.
- `GLF`: Great Lakes open lake/open water forecast, configured by `Noaa__OpenWaterZone`.

The scoring function uses parsed marine-product periods when they are available so nearshore and open-water wind/wave conditions can both affect the readiness rating.

Error responses:

- `400 Bad Request`: malformed ZIP code or incomplete/invalid coordinates.
- `404 Not Found`: ZIP code is syntactically valid but cannot be geocoded.

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
  ],
  "marineProducts": [
    {
      "kind": "Nearshore",
      "productCode": "NSH",
      "productName": "Nearshore Marine Forecast",
      "issuingOffice": "KMKX",
      "zone": "LMZ644",
      "issuedAt": "2026-05-10T02:10:00Z",
      "source": "https://api.weather.gov/products/{id}",
      "text": "LMZ644 product section text...",
      "periods": []
    },
    {
      "kind": "Open Water",
      "productCode": "GLF",
      "productName": "Great Lakes Forecast",
      "issuingOffice": "KMKX",
      "zone": "LMZ671",
      "issuedAt": "2026-05-10T02:15:00Z",
      "source": "https://api.weather.gov/products/{id}",
      "text": "LMZ671 open lake product section text...",
      "periods": []
    }
  ]
}
```

## Scoring Rules

- `Good`: assessed nearshore/open-water periods remain below 2 ft waves, below 15 mph wind, and include no hazards.
- `Caution`: next two forecast periods include waves from 2 to 3.5 ft, winds from 15 to 20 mph, or non-severe advisory language.
- `Bad`: next two forecast periods include waves above 3.5 ft, winds above 20 mph, or hazards mentioning small craft, gale, thunder, or storms.
