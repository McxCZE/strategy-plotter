namespace strategy_plotter.CurrencyMathematicalAveraging
{
    class CurrencyMathematicalAveraging : IStrategyPrototype<CurrencyMathematicalAveragingChromosome>
    {
        // State
        double _ep = 0d;
        double _enter = double.NaN;

        // Settings
        double _buyAgressivness;
        double _sellAgressivness;

        public CurrencyMathematicalAveraging()
        {
            _buyAgressivness = 0.1d;
            _sellAgressivness = 0.1d;
        }

        //This is what's it all about
        public double GetSize(double price, double dir, double asset, double budget, double currency)
        {
            var availableCurrency = Math.Max(0, currency);

            
            double size = 0;
            if (double.IsNaN(_enter))
            {
                // initial bet -> buy
                size = (availableCurrency * price) * 0.05;
            } 
            else
            {
                var distPercentage = (Math.Abs(price - _enter) / price) * 100 ;
                if (distPercentage > 100) { distPercentage = 100; }

                var decisionCurveBuy = ((distPercentage * (distPercentage / _buyAgressivness)) / 100);
                var decisionCurveSell = ((distPercentage * (distPercentage - _sellAgressivness)) / 100);

                var assetsToBuy = budget * decisionCurveBuy;
                var assetsToSell = budget * decisionCurveSell;
                var heldAssets = (asset * price);

                //Buy decisions.
                if (dir > 0) {
                    size = Math.Abs((assetsToBuy - heldAssets)) / price;
                    if (size < 0) { size = 0; }
                    //if (price > _enter) { size = 0; }
                }
                //Sell decisions.
                if (dir < 0) {
                    //if (price >= _enter) 
                    //{ size = assetSell; }
                    //else
                    //{ size = 0; }
                    size = Math.Abs((assetsToSell - heldAssets)) / price;
                }
            }

            //Set size positive/negative, based on direction.
            size = size * dir;
            size = Math.Min(size, availableCurrency / price);

            return size;
        }

        public double GetCenterPrice(double price, double asset, double budget, double currency)
        {
            return price;

            //if (double.IsNaN(_enter) || (asset * price) < budget * _minAssetPercOfBudget)
            //{
            //    return price;
            //}

            //var availableCurrency = Math.Max(0, currency - (budget * _dipRescuePercOfBudget));
            //var dist = (_enter - price) / _enter;
            //if (dist >= _dipRescueEnterPriceDistance)
            //{
            //    // Unblock full currency
            //    availableCurrency = currency;
            //}

            //return Math.Min(_enter, availableCurrency * _reductionMidpoint / asset / (1-_reductionMidpoint));
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

                _buyAgressivness = chromosome.buyAgressivness,
                _sellAgressivness = chromosome.sellAgressivness
            };
        }

        public CurrencyMathematicalAveragingChromosome GetAdamChromosome() => new();

        public double Evaluate(IEnumerable<Trade> trades, double budget, long timeFrame)
        {
            //var t = trades.ToList();
            //if (!t.Any()) return 0;

            //// continuity -> stable performance and delivery of budget extra
            //// get profit at least every 14 days
            //var frames = (int)(TimeSpan.FromMilliseconds(timeFrame).TotalDays / 25);
            //var gk = timeFrame / frames;
            //var lastBudgetExtra = 0d;
            //var minFitness = double.MaxValue;

            //for (var i = 0; i < frames; i++)
            //{
            //    var f0 = gk * i;
            //    var f1 = gk * (i + 1);
            //    var frameTrades = t
            //        .SkipWhile(x => x.Time < f0)
            //        .TakeWhile(x => x.Time < f1)
            //        .ToList();

            //    var currentBudgetExtra = frameTrades.LastOrDefault()?.BudgetExtra ?? lastBudgetExtra;
            //    var tradeFactor = 1; // TradeCountFactor(frameTrades);
            //    var fitness = tradeFactor * (currentBudgetExtra - lastBudgetExtra);
            //    if (fitness < minFitness)
            //    {
            //        minFitness = fitness;
            //    }
            //    lastBudgetExtra = currentBudgetExtra;
            //}

            //return minFitness;

            //var t = trades.ToList();
            //return t.Where(x => x.Size != 0).Count();

            var t = trades.ToList();
            return t.Last().BudgetExtra;
        }
    }
}