using MMBotGA.ga;

interface IStrategyPrototype<T> where T : SpreadChromosome
{
    IStrategyPrototype<T> CreateInstance(T chromosome);
    T GetAdamChromosome();
    double GetSize(double price, double asset, double budget, double currency);
    void OnTrade(double price, double asset, double size);
    double Evaluate(IEnumerable<Trade> trades, double budget, long timeFrame);
}
