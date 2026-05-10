# Lake Michigan Fishing Agent

A runnable .NET Aspire + React MVP for planning Lake Michigan salmon and trout trips from NOAA/NWS marine forecast conditions.

## What It Does

- Reads forecast data from browser coordinates, a ZIP code, or a configurable NOAA/NWS endpoint.
- Falls back to a mock provider when live access is disabled or unavailable.
- Normalizes forecast periods into wind, waves, weather summary, and hazards.
- Scores trip readiness as `Good`, `Caution`, or `Bad`.
- Displays current and next forecast periods in a React dashboard.

## Prerequisites

- .NET 8 SDK
- Node.js 18 or newer
- npm 9 or newer

This workspace currently has Node.js and npm available, but `dotnet` is not installed.

## Configuration

The API reads these settings from `src/LakeMichiganFishingAgent.Api/appsettings.json` or environment variables:

- `Noaa__UseMock`: `true` to force bundled mock forecast data; defaults to `false`.
- `Noaa__ForecastUrl`: optional NOAA/NWS JSON forecast endpoint used when no browser coordinates or ZIP code are supplied.
- `Noaa__Location`: Display location.
- `Noaa__Zone`: Marine zone label.

Example live configuration:

```bash
export Noaa__UseMock=false
export Noaa__Location="Lake Michigan near Milwaukee"
export Noaa__Zone="LMZ644"
```

The live provider accepts browser coordinates through `lat` and `lon`, ZIP codes through `zip`, or an explicit `Noaa__ForecastUrl`. Coordinates are resolved through the NWS `points` endpoint. ZIP geocoding uses `api.zippopotam.us` with a small Lake Michigan fallback list for offline demos.

## Run With Aspire

```bash
dotnet restore
dotnet run --project src/LakeMichiganFishingAgent.AppHost
```

Open the Aspire dashboard URL printed by the AppHost. With the included launch profile, the dashboard listens on `http://localhost:15140`. The React app is served by Vite and the API is started as an Aspire project resource.

## Run Services Separately

Start the API:

```bash
dotnet run --project src/LakeMichiganFishingAgent.Api --urls http://localhost:5000
```

Start the React app:

```bash
cd src/web
npm install
npm run dev
```

Open `http://localhost:5173`.

## Validation Commands

```bash
dotnet build
dotnet test
cd src/web
npm install
npm run build
```

## API Contract

The documented contract lives in [docs/api-contract.md](docs/api-contract.md).

Primary endpoint:

```http
GET /api/forecast/trip-readiness
GET /api/forecast/trip-readiness?lat=43.0447&lon=-87.8990
GET /api/forecast/trip-readiness?zip=53202
```

## Architecture Notes

- `LakeMichiganFishingAgent.AppHost`: .NET Aspire orchestration for API and React web app.
- `LakeMichiganFishingAgent.Api`: ASP.NET Core API, forecast providers, normalized domain model, readiness scoring.
- `LakeMichiganFishingAgent.Tests`: xUnit tests for scoring and the HTTP API contract.
- `src/web`: React + Vite frontend.

The backend defaults to mock NOAA data so the demo works offline. Set `Noaa__UseMock=false` and provide `Noaa__ForecastUrl` to use a live NWS JSON endpoint. If live calls fail, the API returns mock data so the UI remains useful.

## Next-Step Backlog

- Replace simple wave parsing with a richer NOAA marine forecast parser.
- Add selectable Lake Michigan ports and zones.
- Include sunrise/sunset and water temperature.
- Add a saved checklist for boat prep and safety gear.
- Add integration tests with canned NOAA JSON fixtures.
- Improve accessibility and add visual regression checks for the dashboard.
