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
using System.Globalization;
using System.Net;
using System.Text.Json;

ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

Ga<EnterPriceAngleStrategy, EnterPriceAngleStrategyChromosome>();

void Ga<T,S>() 
    where T : IStrategyPrototype<S>, new()
    where S : SpreadChromosome
{
    var filename = "KUCOIN_HTR-USDT_10.02.2021_10.02.2022-cut.csv";

    var prices = File
        .ReadAllLines(filename)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => double.Parse(x, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture))
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

    var directory = GetDirectory();

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

            using var writer = File.CreateText(Path.Combine(directory, $"out-{ga.GenerationsNumber:0000}.csv"));
            Evaluate<S>(prices, best.ToRequest(), strategy.CreateInstance(best), writer);
            File.WriteAllText(Path.Combine(directory, $"cfg-{ga.GenerationsNumber:0000}.json"), 
                JsonSerializer.Serialize(best, new JsonSerializerOptions { WriteIndented = true }));
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

static string GetDirectory()
{ 
    var dir = Path.Combine("results", DateTime.Now.ToString("s").Replace(':', '.'));
    var directory = new DirectoryInfo(dir);
    if (!directory.Exists) directory.Create();
    return directory.FullName;
}

void StaticTest()
{
    //var filename = "FTX_DOGE-PERP_03.12.2020_03.12.2021.csv";
    //var filename = "FTX_DOGE-PERP_03.12.2021_01.02.2022.csv";
    var filename = "KUCOIN_HTR-USDT_10.02.2021_10.02.2022-cut.csv";

    var prices = File
        .ReadAllLines(filename)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => double.Parse(x, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture))
        .ToList();
    using var writer = File.CreateText("out.csv");

    var strategy = new EnterPriceAngleStrategy();

    var genTrades = new GenTradesRequest
    {
        BeginTime = 0,
        Stdev = 95.5,
        Sma = 2,
        Mult = 1,
        Mode = "Together",
        Raise = 113.9,
        Fall = 1.6,
        Cap = 58,
        DynMult = true
    };

    Evaluate(prices, genTrades, strategy, writer);
}

double Evaluate<T>(ICollection<double> prices, GenTradesRequest genTrades, IStrategyPrototype<T> strategy, StreamWriter writer)
    where T : SpreadChromosome
{
    var trades = SpreadRunner.GenerateTrades(genTrades, prices).ToDictionary(x => x.Time / 60000, x => x.Price);

    writer?.WriteLine("price,trade,size,cost,asset,currency,equity,enter,budget extra");

    var simulatedTrades = new List<Trade>();
    var ep = 0d;
    var asset = 0d;
    const double budget = 10000d;
    var currency = budget;
    var index = 0;
    var reinvest = false;
    var budgetExtra = 0d;

    foreach (var p in prices)
    {
        var size = 0d;
        var price = p;
        var trade = trades.TryGetValue(index, out var genPrice);
        var tradableCurrency = currency - budgetExtra;

        if (trade)
        {
            price = genPrice;
            size = strategy.GetSize(price, asset, budget, tradableCurrency);
        }

        index++;
        var cost = price * size;
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
        currency -= cost;

        if (!reinvest && currency > budget + budgetExtra)
        {
            budgetExtra = currency - budget;
        }

        if (size != 0)
        {
            strategy.OnTrade(price, asset, size);
        }

        var equity = currency + (asset * price);
        var newAsset = asset + size;
        ep = size >= 0 ? ep + cost : (ep / asset) * newAsset;
        asset = newAsset;

        writer?.Add(price);
        writer?.Add(trade ? price.Ts() : string.Empty);
        writer?.Add(size);
        writer?.Add(cost);
        writer?.Add(asset);
        writer?.Add(currency);
        writer?.Add(equity);
        writer?.Add(asset == 0 ? string.Empty : (ep / asset).Ts());
        writer?.Add(budgetExtra, true);

        if (trade)
        {
            simulatedTrades.Add(new Trade(price, size, cost, asset, currency, equity, budgetExtra));
        }
    }

    return strategy.Evaluate(simulatedTrades);
}

class EnterPriceAngleStrategyChromosome : SpreadChromosome
{
    public EnterPriceAngleStrategyChromosome() : base(false)
    {
        InitialBetPercOfBudget = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 1));

        MaxEnterPriceDistance = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 1));
        PowerMult = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 10));
        PowerCap = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 10));

        Angle = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 90));

        TargetExitPriceDistance = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 1));
        ExitPowerMult = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 10));

        FinalizeGenes();
    }

    public GeneWrapper<double> InitialBetPercOfBudget { get; }

    public GeneWrapper<double> MaxEnterPriceDistance { get; }
    public GeneWrapper<double> PowerMult { get; }
    public GeneWrapper<double> PowerCap { get; }

    public GeneWrapper<double> Angle { get; }

    public GeneWrapper<double> TargetExitPriceDistance { get; }
    public GeneWrapper<double> ExitPowerMult { get; }

    public override IChromosome CreateNew() => new EnterPriceAngleStrategyChromosome();
}

class EnterPriceAngleStrategy : IStrategyPrototype<EnterPriceAngleStrategyChromosome>
{
    double _ep = 0d;
    double _enter = double.NaN;

    double _minAssetPercOfBudget = 0.001;
    double _initialBetPercOfBudget = 0.04;

    double _maxEnterPriceDistance = 0.05;
    double _powerMult = 0.5;
    double _powerCap = 1;

    double _angle //0-90; the higher, the less assets to buy
    {
        set
        {
            var angleRad = value * Math.PI / 180;
            _sqrtTan = Math.Sqrt(Math.Tan(angleRad));
        }
    }
    double _sqrtTan;

    double _targetExitPriceDistance = 0.04;
    double _exitPowerMult = 6;

    public EnterPriceAngleStrategy()
    {
        _angle = 41;
    }

    public double GetSize(double price, double asset, double budget, double currency)
    {
        double size;
        if (double.IsNaN(_enter) || (asset * price) < budget * _minAssetPercOfBudget)
        {
            // initial bet -> buy
            size = (budget * _initialBetPercOfBudget) / price;
        }
        else if (price < _enter)
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

            var dist = (_enter - price) / _enter;
            var norm = dist / _maxEnterPriceDistance;
            var power = Math.Min(Math.Pow(norm, 4) * _powerMult, _powerCap);
            var newSize = candidateSize * power;

            return double.IsNaN(newSize) ? 0 : Math.Max(0, newSize);
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
            _exitPowerMult = chromosome.ExitPowerMult
        };
    }

    public EnterPriceAngleStrategyChromosome GetAdamChromosome() => new();

    public double Evaluate(IEnumerable<Trade> trades)
    {
        var t = trades.ToList();
        if (!t.Any()) return 0;

        return t.Last().BudgetExtra;
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
