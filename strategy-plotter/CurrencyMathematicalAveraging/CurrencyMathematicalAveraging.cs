#define Continuous // Continuous, Dumb, Triangle (Continuous works best).
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
        double _initBet;

        public CurrencyMathematicalAveraging()
        {
            _buyStrength = 0.03d; //default params. (min: 0.001, max : 1)
            _sellStrength = 1.00d; //default params. (min: 0.001, max : 1)
            _initBet = 0.001d;
        }

        //This is what's it all about
        public double GetSize(double price, double dir, double asset, double budget, double currency)
        {
            var availableCurrency = Math.Max(0, currency);
            double initialBet = ((_initBet / 100) * budget) / price;
            double size = 0;
            bool alert = true;

            if (double.IsNaN(_enter) || _enter == 0) // size < minSize
            {
                size = (budget / price) * 0.001; // <- 0,1% of budget init size.

                if (initialBet > size) { size = initialBet; }
                if (dir < 0) { size = 0; alert = false; }

                if (dir != 0 && Math.Sign(dir) != Math.Sign(size)) size *= -1;
            }
            else
            {
                double distEnter = 0;
                var pnl = (asset * price) - (asset * _enter);

                if (_enter > price) { distEnter = (_enter - price) / _enter; }
                if (_enter < price) { distEnter = (price - _enter) / price; }


                if (distEnter > 1) distEnter = 1;

                double cfgSellStrength = _sellStrength;
                double cfgBuyStrength = _buyStrength;

                if (cfgSellStrength == 0 || cfgSellStrength <= 0 || double.IsNaN(cfgSellStrength))
                { cfgSellStrength = 0; }
                if (cfgBuyStrength == 0 || cfgBuyStrength <= 0 || double.IsNaN(cfgBuyStrength))
                { cfgBuyStrength = 0; }

                if (cfgSellStrength == 1 || cfgSellStrength >= 1)
                { cfgSellStrength = 1; }
                if (cfgBuyStrength == 1 || cfgBuyStrength >= 1)
                { cfgBuyStrength = 1; }

                //Parabola + Sinus
                double sellStrength = Math.Sin(Math.Pow(distEnter, 2) + (Math.PI)) / Math.Pow(1 - cfgSellStrength, 4) + 1;
                double buyStrength = Math.Sin(Math.Pow(distEnter, 2)) / Math.Pow(1 - cfgBuyStrength, 4);


                if (buyStrength < 0) buyStrength = 0;
                if (buyStrength > 1) buyStrength = 1;
                if (double.IsNaN(buyStrength)) buyStrength = 0;

                if (sellStrength < 0) sellStrength = 0;
                if (sellStrength > 1) sellStrength = 1;
                if (double.IsNaN(sellStrength)) sellStrength = 0;

                double assetsToHoldWhenBuying = (budget * buyStrength) / price;
                double assetsToHoldWhenSelling = (budget * sellStrength) / price;

                
                if (dir > 0 && _enter > price)
                {
                    size = assetsToHoldWhenBuying - asset;
                    if (size < 0) { 
                        //size = minSize; 
                        alert = false; 
                    } //Do not buy;
                    if (size * price > availableCurrency) { 
                        size = availableCurrency / price;
                    }
                }

                if (dir < 0 && _enter < price)
                {
                    size = assetsToHoldWhenSelling - asset; // Tady to je rozprcany, neumim prodavat.
                    if (size < 0) { 
                        size = asset;
                    } //Sell everything then;

                    if (size > asset) { 
                        size = 0; 
                        alert = false;
                    }
                    size = size * dir;
                    //if ((size * -1) > asset) { size = asset * -1; }
                }

                if (dir > 0 && _enter < price) {
                    alert = false; 
                }

                if (dir < 0 && _enter > price) {
                    alert = false;
                }

                if (pnl < 0 && dir < 0) { size = 0; }

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
                _sellStrength = chromosome.SellStrength,
                _initBet = chromosome.InitBet
            };
        }

        public CurrencyMathematicalAveragingChromosome GetAdamChromosome() => new();

        public double Evaluate(IEnumerable<Trade> trades, double budget, long timeFrame)
        {

#if Continuous
            var t = trades.ToList();
            if (!t.Any()) return 0;
            // Kde Currency padlo pod 25% budgetu zahoď. 
            if (t.Where(x => x.Currency < (budget * 0.25d)).Count() > 0) return 0;

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
            if ( t.Where( x=> x.Size == 0).Count() / t.Count() > 0.5) { return 0; }

            //if (t.Where(x => x.Currency < (budget * 0.95d)).Count() > 0) return 0;

            //var frames = (int)(TimeSpan.FromMilliseconds(timeFrame).TotalDays / 4);
            //var gk = timeFrame / frames;
            //var minFitness = double.MaxValue;

            //for (var i = 0; i < frames; i++)
            //{
            //    var f0 = gk * i;
            //    var f1 = gk * (i + 1);
            //    var frameTrades = t
            //        .SkipWhile(x => x.Time < f0)
            //        .TakeWhile(x => x.Time < f1)
            //        .ToList();

            //    //var ftCount = frameTrades.Where(x => x.Size != 0).Count();
                
            //    double profit = frameTrades.LastOrDefault()?.BudgetExtra ?? 0;
            //    double backtestInterval = 4;

            //    double sideA = backtestInterval; //- (backtestInterval * (poměr));
            //    double sideB = profit;
            //    double fitnessAngle = Math.Atan2(sideB, sideA) * 180.0d / Math.PI;

            //    var fitness = fitnessAngle;
            //    if (fitness < minFitness)
            //    {
            //        minFitness = fitness;
            //    }
            //}

            double profit = t.Last().BudgetExtra;
            double backtestInterval = (t.Last().Time - t.First().Time) / 86400000d;

            double sideA = backtestInterval; //- (backtestInterval * (poměr));
            double sideB = profit;
            double fitnessAngle = Math.Atan2(sideB, sideA) * 180.0d / Math.PI;

            var fitness = fitnessAngle;

            return fitness;
#else
            var t = trades.ToList();
            if (!t.Any()) return 0;

            return t.Where(x => x.Size != 0).Count();
#endif
        }
    }
}