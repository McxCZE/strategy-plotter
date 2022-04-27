namespace strategy_plotter.Levels
{
    class LevelsStrategy : IStrategyPrototype<LevelsStrategyChromosome>
    {
        public IStrategyPrototype<LevelsStrategyChromosome> CreateInstance(LevelsStrategyChromosome chromosome)
        {
            throw new NotImplementedException();
        }

        public double Evaluate(IEnumerable<Trade> trades, double budget, long timeFrame)
        {
            throw new NotImplementedException();
        }

        public LevelsStrategyChromosome GetAdamChromosome()
        {
            throw new NotImplementedException();
        }

        public double GetCenterPrice(double price, double asset, double budget, double currency)
        {
            throw new NotImplementedException();
        }

        public double GetSize(double price, double dir, double asset, double budget, double currency)
        {
            throw new NotImplementedException();
        }

        public void OnTrade(double price, double asset, double size, double currency)
        {
            throw new NotImplementedException();
        }
    }
}