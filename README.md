# GamesInfoSys

Game information aggregation website built with ASP.NET Core Razor Pages.

## Run

From the project folder:

```powershell
cd .\GamesInfoSys\
dotnet run
```

Then open the printed URL (usually `https://localhost:xxxx`).

## Live data (RAWG)

By default the app runs in **demo mode** using `GamesInfoSys/Data/demo-games.json`.

To enable live data, set a RAWG API key:

- `GamesInfoSys/appsettings.json` → `Rawg:ApiKey`
- or environment variable `RAWG__APIKEY`

## Pricing regions

- Default region is Ukraine: `Pricing:DefaultRegion = "UA"`
- Nintendo Switch override uses South Africa: `Pricing:PlatformRegions:Switch = "ZA"`

## Price tracking (MVP)

This MVP stores offers in a local SQLite file `app.db`.

1) Open a game details page.
2) Paste a Steam App ID (example: `1245620`) and click “Save & refresh”.
3) The page shows the current Steam price for region `UA` and keeps a history table in the database.
