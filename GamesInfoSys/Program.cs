using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();

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

builder.Services.AddHttpClient<GamesInfoSys.Services.SteamStoreClient>(client =>
{
    client.BaseAddress = new Uri("https://store.steampowered.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<GamesInfoSys.Services.OfferAggregator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GamesInfoSys.Data.AppDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
