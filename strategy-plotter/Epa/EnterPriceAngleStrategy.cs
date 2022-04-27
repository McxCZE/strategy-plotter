namespace strategy_plotter.Epa
{
    class EnterPriceAngleStrategy : IStrategyPrototype<EnterPriceAngleStrategyChromosome>
    {
        // State
        double _ep = 0d;
        double _enter = double.NaN;

        // Settings
        double _minAssetPercOfBudget = 0.001;
        double _initialBetPercOfBudget = 0.00972788780927658;

        double _maxEnterPriceDistance = 0.005408694734796882;
        double _powerMult = 7.437917161732912;
        double _powerCap = 8.70842173229903;

        double _angle //0-90; the higher, the less assets to buy
        {
            set
            {
                var angleRad = value * Math.PI / 180;
                _sqrtTan = Math.Sqrt(Math.Tan(angleRad));
            }
        }
        double _sqrtTan;

        double _targetExitPriceDistance = 0.015694422647356987;
        double _exitPowerMult = 6.586619149893522;

        double _reductionMidpoint = 0.7729685984551907;

        double _dipRescuePercOfBudget = 0.5; //0.5 - 1
        double _dipRescueEnterPriceDistance = 0.10; //0.2 - 2

        public EnterPriceAngleStrategy()
        {
            _angle = 10.999484225176275;
        }

        public double GetSize(double price, double dir, double asset, double budget, double currency)
        {
            var availableCurrency = Math.Max(0, currency - budget * _dipRescuePercOfBudget);

            double size;
            if (double.IsNaN(_enter) || asset * price < budget * _minAssetPercOfBudget * (1 - _dipRescuePercOfBudget))
            {
                // initial bet -> buy
                size = availableCurrency * _initialBetPercOfBudget / price;

                // need to indicate sell in case the price grows, but we need to buy
                if (dir != 0 && Math.Sign(dir) != Math.Sign(size)) size *= -1;
            }
            else if (price < _enter)
            {
                var dist = (_enter - price) / _enter;
                if (dist >= _dipRescueEnterPriceDistance)
                {
                    // Unblock full currency
                    availableCurrency = currency;
                }

                //currency / price # buy power
                var half = (availableCurrency / price + asset) * _reductionMidpoint;
                var hhSize = half - asset;

                // sell to reduce position
                if (half < asset)
                {
                    size = hhSize;
                }
                else
                {
                    // buy to lower enter price
                    // https://www.desmos.com/calculator/na4ovcuavg
                    // https://www.desmos.com/calculator/rkw80qbgp3
                    // a: _ep
                    // b: price
                    // c: asset
                    // d: target angle
                    // x: size

                    // calculate recommended price based on preference of cost to reduction ratio
                    var cost = Math.Sqrt(_ep) / _sqrtTan;
                    var candidateSize = cost / price;

                    var norm = dist / _maxEnterPriceDistance;
                    var power = Math.Min(Math.Pow(norm, 4) * _powerMult, _powerCap);
                    var newSize = candidateSize * power;

                    size = double.IsNaN(newSize) ? 0 : Math.Max(0, Math.Min(hhSize, newSize));
                }
            }
            else
            {
                // sell?
                var dist = (price - _enter) / price;
                var norm = dist / _targetExitPriceDistance;
                var power = Math.Pow(norm, 4) * _exitPowerMult;
                size = -asset * power;
            }

            if (size > 0)
            {
                size = Math.Min(size, availableCurrency / price);
            }

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

        public IStrategyPrototype<EnterPriceAngleStrategyChromosome> CreateInstance(EnterPriceAngleStrategyChromosome chromosome)
        {
            return new EnterPriceAngleStrategy
            {
                _ep = 0,
                _enter = double.NaN,

                _initialBetPercOfBudget = chromosome.InitialBetPercOfBudget,

                _maxEnterPriceDistance = chromosome.MaxEnterPriceDistance,
                _powerMult = chromosome.PowerMult,
                _powerCap = chromosome.PowerCap,

                _angle = chromosome.Angle,

                _targetExitPriceDistance = chromosome.TargetExitPriceDistance,
                _exitPowerMult = chromosome.ExitPowerMult,

                _reductionMidpoint = chromosome.ReductionMidpoint,

                _dipRescuePercOfBudget = chromosome.DipRescuePercOfBudget,
                _dipRescueEnterPriceDistance = chromosome.DipRescueEnterPriceDistance,
            };
        }

        public EnterPriceAngleStrategyChromosome GetAdamChromosome() => new();

        public double Evaluate(IEnumerable<Trade> trades, double budget, long timeFrame)
        {
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

            return minFitness;
            //return factor * t.Last().BudgetExtra;
        }

        private static double TradeCountFactor(ICollection<Trade> tr)
        {
            if (tr.Count < 2) return 0;
            var last = tr.Last();
            var first = tr.First();

            var trades = tr.Count(x => x.Size != 0);
            var alerts = tr.Count - trades;

            if (trades == 0) return 0;

            var days = (last.Time - first.Time) / 86400000d;
            var tradesPerDay = trades / days;

            const int mean = 18;
            const int delta = 13; // target trade range is 5 - 31 trades per day

            var x = Math.Abs(tradesPerDay - mean); // 0 - inf, 0 is best
            var y = Math.Max(x - delta, 0) + 1; // 1 - inf, 1 is best ... 
            var r = 1 / y;

            var alertsFactor = Math.Pow(1 - alerts / (double)tr.Count, 5);
            return r * alertsFactor;
        }
    }
}