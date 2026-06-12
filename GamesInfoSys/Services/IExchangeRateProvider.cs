namespace GamesInfoSys.Services;

public interface IExchangeRateProvider
{
    Task<Dictionary<string, decimal>> GetRatesToUahAsync();
}
