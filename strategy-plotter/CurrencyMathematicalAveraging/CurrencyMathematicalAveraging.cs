#define Triangle // Continuous, Dumb, Triangle
namespace strategy_plotter.CurrencyMathematicalAveraging
{
    class CurrencyMathematicalAveraging : IStrategyPrototype<CurrencyMathematicalAveragingChromosome>
    {
        // State
        double _ep = 0d;
        double _enter = double.NaN;

        // Settings
        double _buyStrength; // scaling 0.001
        double _sellStrength; // same

        public CurrencyMathematicalAveraging()
        {
            _buyStrength = 0.03d; //default params. (min: 0.001, max : 1)
            _sellStrength = 1.00d; //default params. (min: 0.001, max : 1)
        }

        //This is what's it all about
        public double GetSize(double price, double dir, double asset, double budget, double currency)
        {
            var availableCurrency = Math.Max(0, currency);

            double size = 0;

            if (double.IsNaN(_enter) || (asset * price) < (budget * 0.01))
            {
                // initial bet -> buy
                size = (budget / price) * 0.05;

                if (dir != 0 && Math.Sign(dir) != Math.Sign(size)) size *= -1;
            }
            else
            {

                double distPercentage = Math.Abs(price - _enter) / price; // Divided by 2 means, 0.5 = 100% distPercentage, 3 eq 0.25 = 100% dist percentage.
                if (distPercentage > 1) distPercentage = 1;

                double buyStrength = distPercentage * (distPercentage / _buyStrength);
                double sellStrength = distPercentage * (distPercentage / _sellStrength);

                if (buyStrength < 0) buyStrength = 0;
                else if (buyStrength > 1) buyStrength = 1;
                else if (double.IsNaN(buyStrength)) buyStrength = 0;

                var assetsToHoldWhenBuying = Math.Abs((budget * buyStrength) / price);
                var assetsToHoldWhenSelling = Math.Abs((budget * sellStrength) / price);

                if (dir > 0) size = Math.Abs(assetsToHoldWhenBuying - asset);
                if (dir < 0) size = Math.Abs(assetsToHoldWhenSelling - asset);

                // if assets that I currently hold are bigger, then the assets I should be holding, therefore "selling", when
                // direction is bottom, do not change, and vice versa.
                if (asset > assetsToHoldWhenBuying && dir > 0) size = 0;
                if (asset < assetsToHoldWhenSelling && dir < 0) size = 0;
                var pnl = (asset * price) - (asset * _enter);
                if (pnl < 0 && dir < 0) { size = 0; }

                size = size * dir;
            }
            return size;
        }

        public double GetCenterPrice(double price, double asset, double budget, double currency)
        {
            return price; 
        }

        public void OnTrade(double price, double asset, double size, double currency)
        {
            var newAsset = asset + size;
            _ep = size >= 0 ? _ep + price * size : _ep / asset * newAsset;
            _enter = _ep / newAsset;
        }

        public IStrategyPrototype<CurrencyMathematicalAveragingChromosome> CreateInstance(CurrencyMathematicalAveragingChromosome chromosome)
        {
            return new CurrencyMathematicalAveraging
            {
                _ep = 0,
                _enter = double.NaN,

                _buyStrength = chromosome.BuyStrength,
                _sellStrength = chromosome.SellStrength
            };
        }

        public CurrencyMathematicalAveragingChromosome GetAdamChromosome() => new();

        public double Evaluate(IEnumerable<Trade> trades, double budget, long timeFrame)
        {

#if Continuous
            var t = trades.ToList();
            if (!t.Any()) return 0;

            // continuity -> stable performance and delivery of budget extra
            // get profit at least every 14 days
            var frames = (int)(TimeSpan.FromMilliseconds(timeFrame).TotalDays / 25);
            var gk = timeFrame / frames;
            var lastBudgetExtra = 0d;
            var minFitness = double.MaxValue;

            for (var i = 0; i < frames; i++)
            {
                var f0 = gk * i;
                var f1 = gk * (i + 1);
                var frameTrades = t
                    .SkipWhile(x => x.Time < f0)
                    .TakeWhile(x => x.Time < f1)
                    .ToList();

                var currentBudgetExtra = frameTrades.LastOrDefault()?.BudgetExtra ?? lastBudgetExtra;
                var tradeFactor = 1; // TradeCountFactor(frameTrades);
                var fitness = tradeFactor * (currentBudgetExtra - lastBudgetExtra);
                if (fitness < minFitness)
                {
                    minFitness = fitness;
                }
                lastBudgetExtra = currentBudgetExtra;
            }

            double maxCost = 0;
            double cost = 0;

            foreach (var trade in t)
            {
                cost += trade.Size * trade.Price;
                if (cost > maxCost) { maxCost = cost; }

                if (maxCost > budget * 0.5d) { return 0; }
            }

            return minFitness;
#elif Dumb
            var t = trades.ToList();
            if (!t.Any()) return 0;

            double maxCost = 0;
            double cost = 0;

            foreach (var trade in t)
            {
                cost += trade.Size * trade.Price;
                if (cost > maxCost) { maxCost = cost; }

                if (maxCost > budget * 0.70d) { return 0; }
            }

            return t.Last().BudgetExtra;
#elif Triangle
            var t = trades.ToList();

            //Cut out useless backtests.
            if (!t.Any()) return 0;

            //double tPerDay = t.Where(x => x.Size != 0).Count() / ((t.Last().Time - t.First().Time) / 86400000d);
            //if (tPerDay < 5) return 0;

            double maxCost = 0;
            double cost = 0;
            foreach (var trade in t)
            {
                cost += trade.Size * trade.Price;
                if (cost > maxCost) { maxCost = cost; }

                if (maxCost > budget * 0.70d) { 
                    return 0;
                }
            }
            //

            double profit = t.Last().BudgetExtra;
            double backtestInterval = (t.Last().Time - t.First().Time) / 86400000d;
            double alertRatio = 1 - (t.Where(x => x.Size == 0).Count() / t.Count());

            double sideA = backtestInterval - (backtestInterval * (alertRatio / 2));
            double sideB = profit;
            double fitnessAngle = Math.Atan2(sideB, sideA) * 180.0d / Math.PI;

            return fitnessAngle;
#endif
        }
    }
}