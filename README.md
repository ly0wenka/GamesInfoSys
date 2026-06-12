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

By default the app runs in demo mode using `GamesInfoSys/Data/demo-games.json`.

To enable live data, set a RAWG API key:

- `GamesInfoSys/appsettings.json` -> `Rawg:ApiKey`
- or environment variable `RAWG__APIKEY`

## Pricing regions

- Default region is Ukraine: `Pricing:DefaultRegion = "UA"`
- Nintendo Switch override uses South Africa: `Pricing:PlatformRegions:Switch = "ZA"`
- Preferred display currency: `Pricing:PreferredCurrency = "UAH"` (UI shows approx. UAH conversion when store currency is not UAH)

## Price tracking (MVP)

This MVP stores offers in a local SQLite file `app.db`.

1. Open a game details page.
2. Paste a Steam App ID (example: `1245620`) and click `Save & refresh`.
3. The page shows the current Steam price for region `UA` and keeps a history table in the database.

## UAH conversion

The UI converts non-UAH prices to approximate UAH using cached exchange rates from the National Bank of Ukraine (NBU).
You can change the base URL in `GamesInfoSys/appsettings.json` under `Currency:NbuBaseUrl`.

## UA retailers and YouTube

Each game page includes quick search buttons for `rozetka.com.ua`, `prom.ua`, `olx.ua`, plus YouTube `review` and `gameplay` searches.

## Tests

Run the automated tests from the repo root:

```powershell
dotnet test .\GamesInfoSys.Tests\GamesInfoSys.Tests.csproj -c Release
```

## Entity Framework migrations

The app now uses EF Core migrations at startup via `Database.Migrate()`.

To create a new migration:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add <Name> --project .\GamesInfoSys\GamesInfoSys.csproj --startup-project .\GamesInfoSys\GamesInfoSys.csproj --output-dir Data\Migrations
```
