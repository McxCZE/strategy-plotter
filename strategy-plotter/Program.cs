using GeneticSharp.Domain;
using GeneticSharp.Domain.Chromosomes;
using GeneticSharp.Domain.Crossovers;
using GeneticSharp.Domain.Mutations;
using GeneticSharp.Domain.Populations;
using GeneticSharp.Domain.Randomizations;
using GeneticSharp.Domain.Selections;
using GeneticSharp.Domain.Terminations;
using MMBot.Api.dto;
using MMBotGA.ga;
using MMBotGA.ga.execution;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

Ga<EnterPriceAngleStrategy, EnterPriceAngleStrategyChromosome>();
//StaticTest();

void Ga<T,S>() 
    where T : IStrategyPrototype<S>, new()
    where S : SpreadChromosome
{
    // todo: downloader, chunks
    // todo: fitness max cost

    var filename = "KUCOIN_HTR-USDT_01.04.2021_01.04.2022.csv";
    //var filename = "KUCOIN_VRA-BTC_01.04.2021_01.04.2022.csv";
    //var filename = "KUCOIN_HTR-BTC_23.03.2021_23.03.2022.csv";
    //var filename = "FTX_DOGE-PERP_14.02.2021_14.02.2022.csv";

    var prices = File
        .ReadAllLines(filename)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => double.Parse(x.Split(',')[0], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture))
        .ToList();

    var strategy = new T();
    var selection = new EliteSelection();
    var crossover = new UniformCrossover();
    var mutation = new UniformMutation(true);
    var termination = new FitnessStagnationTermination(80);
    ThreadPool.GetAvailableThreads(out var threads, out _);
    var executor = new ExactParallelTaskExecutor(threads);
    var population = new Population(500, 3000, strategy.GetAdamChromosome());
    var fitness = new DynamicFitness<S>(x => Evaluate<S>(prices, x.ToRequest(), strategy.CreateInstance(x), null));
    var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation)
    {
        Termination = termination,
        TaskExecutor = executor
    };

    var directory = GetDirectory("-HTR-USDT-7k");

    S best = null;
    void OnGenerationRan(object o, EventArgs eventArgs)
    {
        var current = ga.BestChromosome as S;
        if (best == null || current.Fitness > best.Fitness)
        {
            best = current;
            Console.WriteLine();
            Console.WriteLine($"New best ({ga.GenerationsNumber}): {best.Fitness}");
            Console.WriteLine();

            var outCsv = Path.Combine(directory, "out-best.csv");
            var outJson = Path.Combine(directory, "cfg-best.json");
            var outBase64 = Path.Combine(directory, "base64-best.json");

            while (true)
            {
                try
                {
                    using (var writer = File.CreateText(outCsv))
                    {
                        Evaluate<S>(prices, best.ToRequest(), strategy.CreateInstance(best), writer);
                    }
                    File.WriteAllText(outJson,
                        JsonSerializer.Serialize(best, new JsonSerializerOptions { WriteIndented = true }));
                    File.WriteAllText(outBase64,
                        "{{" + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(best.ToConfig(), new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }))) + "}}");

                    File.Copy(outCsv, Path.Combine(directory, $"out-{ga.GenerationsNumber:0000}.csv"), true);
                    File.Copy(outJson, Path.Combine(directory, $"cfg-{ga.GenerationsNumber:0000}.json"), true);
                    File.Copy(outBase64, Path.Combine(directory, $"base64-{ga.GenerationsNumber:0000}.json"), true);
                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine();
                    Thread.Sleep(2000);
                }
            }
        }
        else
        {
            Console.Write('.');
        }
    }

    ga.GenerationRan += OnGenerationRan;
    ga.Start();
    ga.GenerationRan -= OnGenerationRan;
}

static string GetDirectory(string suffix)
{ 
    var dir = Path.Combine("results", DateTime.Now.ToString("s").Replace(':', '.') + suffix);
    var directory = new DirectoryInfo(dir);
    if (!directory.Exists) directory.Create();
    return directory.FullName;
}

void StaticTest()
{
    //var filename = "FTX_DOGE-PERP_03.12.2020_03.12.2021.csv";
    var filename = "KUCOIN_XDB-USDT_01.04.2021_01.04.2022.csv";
    //var filename = "KUCOIN_HTR-USDT_01.01.2020_01.04.2022.csv";
    //var filename = "KUCOIN_FLUX-USDT_01.01.2021_06.03.2022.csv";

    var prices = File
        .ReadAllLines(filename)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => double.Parse(x.Split(',')[0], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture))
        .ToList();
    using var writer = File.CreateText("out.csv");

    var strategy = new EnterPriceAngleStrategy();

    var genTrades = new GenTradesRequest
    {
        BeginTime = 0,
        Stdev = 238.6920513836667,
        Sma = 20.29968843050301,
        Mult = 0.6610092297196388,
        Mode = "Together",
        Raise = 61.17793273227289,
        Fall = 6.3417955216486,
        Cap = 2.8303833678364754,
        DynMult = true,
        SpreadFreeze = true,
        Sliding = false
    };

    var sw = new Stopwatch();
    sw.Restart();
    Evaluate(prices, genTrades, strategy, writer);
    sw.Stop();
    Console.WriteLine($"Done in {sw.ElapsedMilliseconds} ms");
}

double Evaluate<T>(ICollection<double> prices, GenTradesRequest genTrades, IStrategyPrototype<T> strategy, StreamWriter writer)
    where T : SpreadChromosome
{
    writer?.WriteLine("price,hspread,lspread,trade,size,cost,asset,currency,equity,enter,center,budget extra");

    var simulatedTrades = new List<Trade>();
    var ep = 0d;
    var asset = 0d;

    // BTC
    //const double budget = 0.1d;
    //const double minOrderCost = 0.00000001d;
    //const double fixedTradeFee = 0.0000002174d;

    // USD
    const double budget = 7000d;
    const double minOrderCost = 10d;
    const double fixedTradeFee = 0.01d;

    const double tradeFee = 0.002d; // 0.007
    const double tradeRebate = 0; // 0.00025d;
    
    var currency = budget;
    var index = 0;
    var reinvest = false;
    var budgetExtra = 0d;
    var lastPrice = 0d;
    var tradableCurrency = 0d;
    var callback = new DynamicStrategyCallback(
        (price, dir) => strategy.GetSize(price, dir, asset, budget, tradableCurrency),
        price => strategy.GetCenterPrice(price, asset, budget, tradableCurrency)
        );
    var simulator = new Simulator(genTrades, callback, 0);

    //warmup spread
    foreach (var p in prices.Take((int)TimeSpan.FromHours(Math.Max(genTrades.Stdev, genTrades.Sma)).TotalMinutes))
    {
        simulator.Tick(p, true);
    }

    foreach (var p in prices) //todo rev ... not needed for now
    {
        tradableCurrency = currency - budgetExtra;
        var t = simulator.Tick(p, false);
        var size = 0d;
        var price = t.Spread.Price;

        var mode = Math.Sign(lastPrice - price); //-1 sell only; 1 buy only; 0 both
        lastPrice = price;

        var trade = t.trade != null;
        var genPrice = trade ? t.trade.Price : 0;

        if (trade)
        {
            price = genPrice;
            size = strategy.GetSize(price, mode, asset, budget, tradableCurrency);
            if (mode != 0 && Math.Sign(size) != mode)
            {
                size = 0; // cannot execude order in opposite trend as a market maker
            }
        }

        index++;
        var cost = price * size;

        if (Math.Abs(cost) < minOrderCost)
        {
            cost = minOrderCost * Math.Sign(cost);
            size = cost / price;
        }

        if (cost > tradableCurrency)
        {
            cost = tradableCurrency;
            size = cost / price;
        }
        else if (-size > asset)
        {
            size = -asset;
            cost = price * size;
        }

        if (Math.Abs(cost) < minOrderCost)
        {
            size = 0;
            cost = 0;
        }

        currency -= cost;
        currency -= Math.Abs(cost * tradeFee);
        currency += Math.Abs(cost * tradeRebate);
        if (size != 0) currency -= fixedTradeFee;

        if (!reinvest && currency > budget + budgetExtra)
        {
            budgetExtra = currency - budget;
        }

        if (size != 0)
        {
            strategy.OnTrade(price, asset, size);
        }

        var newAsset = asset + size;
        ep = size >= 0 ? ep + cost : (ep / asset) * newAsset;
        asset = newAsset;
        var equity = currency + (asset * price);

        writer?.Add(price);
        writer?.Add(t.Spread.Spread.High);
        writer?.Add(t.Spread.Spread.Low);
        writer?.Add(trade ? price.Ts() : string.Empty);
        writer?.Add(size);
        writer?.Add(cost);
        writer?.Add(asset);
        writer?.Add(currency);
        writer?.Add(equity);
        writer?.Add(asset == 0 ? string.Empty : (ep / asset).Ts());
        writer?.Add(callback.GetCenterPrice(price));
        writer?.Add(budgetExtra, true);

        if (trade)
        {
            simulatedTrades.Add(new Trade(index * 60000L, price, size, cost, asset, currency, equity, budgetExtra));
        }
    }

    return strategy.Evaluate(simulatedTrades, budget, prices.Count * 60000L);
}

class EnterPriceAngleStrategyChromosome : SpreadChromosome
{
    public EnterPriceAngleStrategyChromosome() : base(false)
    {
        InitialBetPercOfBudget = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 1)); //0-1

        MaxEnterPriceDistance = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 0.5));
        PowerMult = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 10));
        PowerCap = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 10));

        Angle = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 70)); //0-90

        TargetExitPriceDistance = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 0.5));
        ExitPowerMult = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 10));

        ReductionMidpoint = Factory.Create(() => RandomizationProvider.Current.GetDouble(0.1, 0.9)); //Factory.Create(0.6d);

        DipRescuePercOfBudget = Factory.Create(0d); //0.5 - 1
        DipRescueEnterPriceDistance = Factory.Create(0.2d); //0.2 - 2

        FinalizeGenes();
    }

    public GeneWrapper<double> InitialBetPercOfBudget { get; }

    public GeneWrapper<double> MaxEnterPriceDistance { get; }
    public GeneWrapper<double> PowerMult { get; }
    public GeneWrapper<double> PowerCap { get; }

    public GeneWrapper<double> Angle { get; }

    public GeneWrapper<double> TargetExitPriceDistance { get; }
    public GeneWrapper<double> ExitPowerMult { get; }

    public GeneWrapper<double> ReductionMidpoint { get; }

    public GeneWrapper<double> DipRescuePercOfBudget { get; }
    public GeneWrapper<double> DipRescueEnterPriceDistance { get; }

    public override IChromosome CreateNew() => new EnterPriceAngleStrategyChromosome();

    public override Config ToConfig()
    {
        var res = base.ToConfig();
        res.Strategy = new EpaStrategyConfig
        {
            Type = "enter_price_angle",
            Angle = Angle,
            ExitPowerMult = ExitPowerMult,
            InitialBetPercOfBudget = InitialBetPercOfBudget,
            MaxEnterPriceDistance = MaxEnterPriceDistance,
            MinAssetPercOfBudget = 0.001,
            PowerCap = PowerCap,
            PowerMult = PowerMult,
            TargetExitPriceDistance = TargetExitPriceDistance,
            ReductionMidpoint = ReductionMidpoint,
            DipRescuePercOfBudget = DipRescuePercOfBudget,
            DipRescueEnterPriceDistance = DipRescueEnterPriceDistance,
            Backtest = false
        };
        return res;
    }
}

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
        var availableCurrency = Math.Max(0, currency - (budget * _dipRescuePercOfBudget));

        double size;
        if (double.IsNaN(_enter) || (asset * price) < budget * _minAssetPercOfBudget)
        {
            // initial bet -> buy
            size = (availableCurrency * _initialBetPercOfBudget) / price;

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
            var half = ((availableCurrency / price) + asset) * _reductionMidpoint;
            var hhSize = half - asset;

            // sell to reduce position
            if (half < asset)
            {
                return hhSize;
            }

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

            return double.IsNaN(newSize) ? 0 : Math.Max(0, Math.Min(hhSize, newSize));
        }
        else
        {
            // sell?
            var dist = (price - _enter) / price;
            var norm = dist / _targetExitPriceDistance;
            var power = Math.Pow(norm, 4) * _exitPowerMult;
            size = -asset * power;
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

    public void OnTrade(double price, double asset, double size)
    {
        var newAsset = asset + size;
        _ep = size >= 0 ? _ep + (price * size) : (_ep / asset) * newAsset;
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

        var alertsFactor = Math.Pow(1 - (alerts / (double)tr.Count), 5);
        return r * alertsFactor;
    }
}

class HalfHalf
{
    public double GetSize(double price, double asset, double budget, double currency)
    {
        return (((currency / price) + asset) * 0.5) - asset;
    }

    public void OnTrade(double price, double size)
    { }
}

public class EpaStrategyConfig
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("exit_power_mult")]
    public double ExitPowerMult { get; set; }

    [JsonPropertyName("initial_bet_perc_of_budget")]
    public double InitialBetPercOfBudget { get; set; }

    [JsonPropertyName("max_enter_price_distance")]
    public double MaxEnterPriceDistance { get; set; }

    [JsonPropertyName("min_asset_perc_of_budget")]
    public double MinAssetPercOfBudget { get; set; }

    [JsonPropertyName("power_cap")]
    public double PowerCap { get; set; }

    [JsonPropertyName("power_mult")]
    public double PowerMult { get; set; }

    [JsonPropertyName("target_exit_price_distance")]
    public double TargetExitPriceDistance { get; set; }

    [JsonPropertyName("angle")]
    public double Angle { get; set; }

    [JsonPropertyName("reduction_midpoint")]
    public double ReductionMidpoint { get; set; }

    [JsonPropertyName("dip_rescue_perc_of_budget")]
    public double DipRescuePercOfBudget { get; set; }

    [JsonPropertyName("dip_rescue_enter_price_distance")]
    public double DipRescueEnterPriceDistance { get; set; }

    [JsonPropertyName("backtest")]
    public bool Backtest { get; set; }
}
