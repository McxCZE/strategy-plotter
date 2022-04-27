using MMBot.Api.dto;
using MMBotGA.ga;

interface IStrategyPrototype<T> where T : SpreadChromosome
{
    IStrategyPrototype<T> CreateInstance(T chromosome);
    T GetAdamChromosome();
    double GetSize(double price, double dir, double asset, double budget, double currency);
    double GetCenterPrice(double price, double asset, double budget, double currency);
    void OnTrade(double price, double asset, double size, double currency);
    double Evaluate(IEnumerable<Trade> trades, double budget, long timeFrame);
}
