namespace GamesInfoSys.Services;

public sealed class CurrencyConverter
{
    private readonly NbuRatesClient _nbu;

    public CurrencyConverter(NbuRatesClient nbu)
    {
        _nbu = nbu;
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

        var rates = await _nbu.GetRatesToUahAsync();
        if (!rates.TryGetValue(fromCurrency, out var rateToUah) || rateToUah <= 0)
            return null;

        return amount * rateToUah;
    }
}

