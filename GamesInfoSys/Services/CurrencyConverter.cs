namespace GamesInfoSys.Services;

public sealed class CurrencyConverter
{
    private readonly IExchangeRateProvider _rates;

    public CurrencyConverter(IExchangeRateProvider rates)
    {
        _rates = rates;
    }

    public async Task<decimal?> ToUahAsync(decimal amount, string fromCurrency)
    {
        if (amount <= 0)
            return null;
        if (string.IsNullOrWhiteSpace(fromCurrency))
            return null;

        fromCurrency = fromCurrency.Trim().ToUpperInvariant();
        if (fromCurrency == "UAH")
            return amount;

        var rates = await _rates.GetRatesToUahAsync();
        if (!rates.TryGetValue(fromCurrency, out var rateToUah) || rateToUah <= 0)
            return null;

        return amount * rateToUah;
    }
}
