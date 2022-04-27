namespace strategy_plotter.Gamma
{
    class GammaStrategy : IStrategyPrototype<GammaStrategyChromosome>
    {
        private record State(
            double K = 0,
            double W = 0,
            double P = 0,
            double B = 0,
            double D = 0,
            double Uv = 0, //unprocessed volume
            double Kk = 0
        );

        private record Config(
            mmbot_microport.Strategy.IntegrationTable IntTable,
            int ReductionMode,
            double Trend,
            bool Reinvest,
            bool Maxrebalance
        );

        private record NNRes(
            double K,
            double W
        );

        private Config _cfg;
        private State _state;

        public IStrategyPrototype<GammaStrategyChromosome> CreateInstance(GammaStrategyChromosome chromosome)
        {
            return new GammaStrategy
            {
                _cfg = new Config(
                    new mmbot_microport.Strategy.IntegrationTable(Enum.Parse<mmbot_microport.Strategy.IntegrationTable.Function>(chromosome.Function, true), chromosome.Exponent),
                    chromosome.Rebalance,
                    chromosome.Trend,
                    false, // no reinvest
                    false
                ),
                _state = new State()
            };
        }

        public double Evaluate(IEnumerable<Trade> trades, double budget, long timeFrame)
        {
            // todo: better fitness for gamma

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
        }

        public GammaStrategyChromosome GetAdamChromosome() => new();

        public double GetCenterPrice(double price, double asset, double budget, double currency)
        {
            return asset == 0 ? price : GetEquilibrium(asset);
        }

        public double GetEquilibrium(double assets)
        {
            var a = _cfg.IntTable.CalcAssets(_state.Kk, _state.W, _state.P);
            if (assets > a)
            {
                return mmbot_microport.Strategy.Numerical.NumericSearchR1(_state.P,
                    price => _cfg.IntTable.CalcAssets(_state.Kk, _state.W, price) - assets);
            }

            if (assets < a)
            {
                return mmbot_microport.Strategy.Numerical.NumericSearchR2(_state.P,
                    price => _cfg.IntTable.CalcAssets(_state.Kk, _state.W, price) - assets);
            }

            return _state.P;
        }

        private double CalculatePosition(double a, double price)
        {
            var newk = CalculateNewNeutral(a, price);
            var newkk = CalibK(newk.K);
            return _cfg.IntTable.CalcAssets(newkk, newk.W, price);
        }

        public double GetSize(double newPrice, double dir, double assets, double budget, double currency)
        {
            var newPosz = CalculatePosition(assets, newPrice);
            var dffz = newPosz - assets;
            return dir switch
            {
                < 0 when dffz == 0 && newPosz == 0 => 0,
                > 0 when dffz == 0 && newPosz == 0 => 0, // todo: min size allowed by market
                _ => dffz
            };
        }

        private bool IsValid()
        {
            return _state.K > 0 && _state.P > 0 && _state.W > 0 && _state.B > 0;
        }
        private double CalibK(double k)
        {
            if (_cfg.Maxrebalance) return k / _cfg.IntTable.GetMin();
            var l = -_cfg.Trend / 100.0;
            var kk = Math.Pow(Math.Exp(-1.6 * l * l + 3.4 * l), 1.0 / _cfg.IntTable.Z);
            return k / kk;
        }

        private void Init(double price, double assets, double currency)
        {
            var budget = _state.B > 0 ? _state.B : assets * price + currency;
            if (budget <= 0) throw new InvalidOperationException("No budget");
            if (_state.P != 0) price = _state.P;
            var newstP = price;
            var newstK = 0d;
            if (newstP <= 0) throw new InvalidOperationException("Invalid price");
            if (assets <= 0) newstK = price;
            else
            {
                var r = assets * price / budget;
                var k = price / _cfg.IntTable.B; //assume that k is lowest possible value;
                var a = _cfg.IntTable.CalcAssets(CalibK(k), 1, price);
                var b = _cfg.IntTable.CalcBudget(CalibK(k), 1, price);
                var r0 = a / b * price;
                if (r > r0)
                {
                    if (r < 0.5 || _cfg.IntTable.Fn != mmbot_microport.Strategy.IntegrationTable.Function.Halfhalf)
                    {
                        newstK = mmbot_microport.Strategy.Numerical.NumericSearchR2(k, k1 => {
                            var a1 = _cfg.IntTable.CalcAssets(CalibK(k1), 1, price);
                            var b1 = _cfg.IntTable.CalcBudget(CalibK(k1), 1, price);
                            if (b1 <= 0) return double.MaxValue;
                            if (a1 <= 0) return 0.0;
                            return a1 / b1 * price - r;
                        });
                    }
                    else
                    {
                        newstK = (price / _cfg.IntTable.A) / CalibK(1.0);
                        budget = 2 * assets * price;
                    }
                }
                else
                {
                    newstK = price;
                }
            }
            var newstKk = CalibK(newstK);
            var w1 = _cfg.IntTable.CalcBudget(newstKk, 1.0, price);

            _state = new State
            {
                P = newstP,
                K = newstK,
                Kk = newstKk,
                W = budget / w1,
                B = budget,
                D = 0,
                Uv = 0
            };
        }

        //double tradePrice, double tradeSize, double assetsLeft, double currencyLeft
        public void OnTrade(double tradePrice, double assetsLeft, double tradeSize, double currencyLeft)
        {
            if (!IsValid()) Init(tradePrice, assetsLeft, currencyLeft);

            var curPos = assetsLeft - tradeSize;

            var nn = CalculateNewNeutral(curPos, tradePrice);
            if (tradeSize == 0 && Math.Abs(nn.K - tradePrice) > Math.Abs(_state.K - tradePrice))
            {
                nn = new NNRes(_state.K, _state.W);
            }
            var newkk = CalibK(nn.K);
            var volume = -tradePrice * tradeSize;
            var calcPos = _cfg.IntTable.CalcAssets(newkk, nn.W, tradePrice);
            var unprocessed = (assetsLeft - calcPos) * tradePrice;
            var prevCalcPos = _cfg.IntTable.CalcAssets(_state.Kk, _state.W, _state.P);
            var prevUnprocessed = (assetsLeft - tradeSize - prevCalcPos) * _state.P;
            var prevCur = _state.B - prevCalcPos * _state.P - prevUnprocessed;
            var bn = _cfg.IntTable.CalcBudget(newkk, nn.W, tradePrice);
            var newCur = bn - calcPos * tradePrice - unprocessed;
            var np = volume - newCur + prevCur;
            var neww = nn.W;
            var d = _state.D;

            if (_cfg.Reinvest && tradeSize != 0)
            {
                d += np;
                if (d > 0)
                {
                    neww = nn.W * (bn + d) / bn;
                    d = 0;
                }
                bn = _cfg.IntTable.CalcBudget(newkk, neww, tradePrice);
            }

            _state = new State(nn.K, neww, tradePrice, bn, d, unprocessed, newkk);
        }

        private NNRes CalculateNewNeutral(double a, double price)
        {
            if ((price - _state.K) * (_state.P - _state.K) < 0)
            {
                return new NNRes(_state.K, _state.W);
            }
            var pnl = a * (price - _state.P);
            var w = _state.W;
            var mode = _cfg.ReductionMode;
            if (price < _state.K && !_cfg.Maxrebalance && (mode == 0
                || (mode == 1 && price > _state.P)
                || (mode == 2 && price < _state.P))) return new NNRes(_state.K, w);

            double bc;
            double needb;
            double newk;
            if (price > _state.K)
            {

                if (price < _state.P && _cfg.Maxrebalance)
                {
                    bc = _cfg.IntTable.CalcBudget(_state.Kk, _state.W, _state.P);
                    needb = pnl + bc;
                    w = mmbot_microport.Strategy.Numerical.NumericSearchR2(0.5 * _state.W,
                        w1 => _cfg.IntTable.CalcBudget(_state.Kk, w1, price) - needb);
                    newk = _state.K;
                }
                else
                {
                    bc = _cfg.IntTable.CalcBudget(_state.Kk, _state.W, price);
                    needb = bc - pnl;
                    newk = mmbot_microport.Strategy.Numerical.NumericSearchR2(0.5 * _state.K,
                        k => _cfg.IntTable.CalcBudget(CalibK(k), _state.W, _state.P) - needb);
                    if (newk < _state.K /*&& cfg.intTable->calcAssets(newk, state.w, price)<min_order_size*/)
                        newk = (3 * _state.K + price) * 0.25;
                }
            }
            else if (price < _state.K)
            {
                if (_cfg.Maxrebalance && price > _state.P)
                {
                    var k = price * 0.1 + _state.K * 0.9;//*cfg.intTable->GetMin();
                    var kk = CalibK(k);
                    var w1 = _cfg.IntTable.CalcAssets(kk, 1.0, price);
                    var w2 = _cfg.IntTable.CalcAssets(_state.Kk, _state.W, price);
                    var neww = w2 / w1;
                    if (neww > w * 2)
                    {
                        return new NNRes(_state.K, _state.W);
                    }
                    w = neww;
                    newk = k;
                    //var pos1 = cfg.intTable.calcAssets(state.kk, state.w, price);
                    //var pos2 = cfg.intTable.calcAssets(kk, w, price);
                    //var b1 = cfg.intTable.calcBudget(state.kk, state.w, price);
                    //var b2 = cfg.intTable.calcBudget(kk, w, price);
                    //logDebug("Rebalance POS: $1 > $2, BUDGET: $3 > $4", pos1, pos2, b1, b2);
                }
                else
                {
                    bc = _cfg.IntTable.CalcBudget(_state.Kk, _state.W, _state.P);
                    needb = bc + pnl;
                    if (mode == 4 && price / _state.Kk > 1.0)
                    {
                        var spr = price / _state.P;
                        var reff = _cfg.IntTable.CalcAssets(_state.Kk, _state.W, _state.K) * _state.K * (spr - 1.0)
                            + _cfg.IntTable.CalcBudget(_state.Kk, _state.W, _state.K) - _cfg.IntTable.CalcBudget(_state.Kk, _state.W, _state.K * spr);
                        //double bq = cfg.intTable->calcBudget(state.kk, state.w, price);
                        //				double maxref = needb - bq;
                        //ref = std::min(ref, maxref);
                        needb -= reff;
                    }

                    newk = mmbot_microport.Strategy.Numerical.NumericSearchR1(1.5 * _state.K,
                        k => _cfg.IntTable.CalcBudget(CalibK(k), _state.W, price) - needb);
                }
            }
            else
            {
                newk = _state.K;
            }
            if (newk < 1e-100 || newk > 1e+100) newk = _state.K;
            return new NNRes(newk, w);
        }
    }
}