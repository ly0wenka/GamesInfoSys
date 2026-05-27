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

