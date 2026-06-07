using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddLocalization();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<GamesInfoSys.Services.UiText>();

builder.Services.Configure<GamesInfoSys.Services.RawgOptions>(builder.Configuration.GetSection("Rawg"));
builder.Services.AddHttpClient<GamesInfoSys.Services.RawgClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GamesInfoSys.Services.RawgOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.Configure<GamesInfoSys.Services.PricingOptions>(builder.Configuration.GetSection("Pricing"));
builder.Services.AddSingleton<GamesInfoSys.Services.RegionResolver>();

builder.Services.AddDbContext<GamesInfoSys.Data.AppDbContext>(o =>
{
    o.UseSqlite("Data Source=app.db");
});

builder.Services.Configure<GamesInfoSys.Services.CurrencyOptions>(builder.Configuration.GetSection("Currency"));
builder.Services.AddHttpClient<GamesInfoSys.Services.NbuRatesClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GamesInfoSys.Services.CurrencyOptions>>().Value;
    client.BaseAddress = new Uri(options.NbuBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<GamesInfoSys.Services.CurrencyConverter>();

builder.Services.Configure<GamesInfoSys.Services.ScrapingOptions>(builder.Configuration.GetSection("Scraping"));
builder.Services.AddHttpClient<GamesInfoSys.Services.UaMarketplaceScraper>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("GamesInfoSys/1.0 (UA price tracker; scraping MVP)");
    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("uk-UA,uk;q=0.9,en;q=0.6");
});

builder.Services.AddHttpClient<GamesInfoSys.Services.SteamStoreClient>(client =>
{
    client.BaseAddress = new Uri("https://store.steampowered.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient<GamesInfoSys.Services.CheapSharkClient>(client =>
{
    client.BaseAddress = new Uri("https://www.cheapshark.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
    // CheapShark requires a descriptive User-Agent to avoid accidental blocking.
    client.DefaultRequestHeaders.UserAgent.ParseAdd("GamesInfoSys/1.0 (no-key; marketplace redirects)");
});
builder.Services.AddScoped<GamesInfoSys.Services.OfferAggregator>();

var app = builder.Build();

var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("uk")
};

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};

localizationOptions.RequestCultureProviders.Insert(0, new QueryStringRequestCultureProvider());

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRequestLocalization(localizationOptions);

app.UseRouting();

app.UseAuthorization();

app.MapGet("/set-language", (HttpContext httpContext, string culture, string? returnUrl) =>
{
    var normalizedCulture = supportedCultures.Any(x => string.Equals(x.Name, culture, StringComparison.OrdinalIgnoreCase))
        ? culture
        : "en";

    httpContext.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(normalizedCulture)),
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            Path = "/"
        });

    return Results.LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
});

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GamesInfoSys.Data.AppDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
