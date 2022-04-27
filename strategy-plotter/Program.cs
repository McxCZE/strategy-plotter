#define GA    // GA, STATIC
#define USD   // USD, BTC
#define CMA // GAMMA, LEVELS, CMA

using GeneticSharp.Domain;
using GeneticSharp.Domain.Crossovers;
using GeneticSharp.Domain.Mutations;
using GeneticSharp.Domain.Populations;
using GeneticSharp.Domain.Selections;
using GeneticSharp.Domain.Terminations;
using MMBot.Api.dto;
using MMBotGA.ga;
using MMBotGA.ga.execution;
using strategy_plotter.Epa;
using strategy_plotter.CurrencyMathematicalAveraging;
using strategy_plotter.Gamma;
using strategy_plotter.Levels;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

#region GA data
//var filename = "KUCOIN_XDB-USDT_21.04.2021_21.04.2022.csv";
var filename = "FTX_ALPHA-PERP_21.03.2021_21.04.2022.csv";
//var filename = "KUCOIN_VRA-BTC_01.04.2021_01.04.2022.csv";
//var filename = "KUCOIN_HTR-BTC_23.03.2021_23.03.2022.csv";
//var filename = "FTX_DOGE-PERP_14.02.2021_14.02.2022.csv";

var outputSuffix = "-ALPHA-CMA-";
#endregion

#region Configuration for static test
var configFile = "base64-best.json";
#endregion

#region Market info
#if BTC
const double budget = 0.1d;
const double minOrderCost = 0.00000001d;
const double fixedTradeFee = 0.0000002174d;

#elif USD
const double budget = 1000d;
const double minOrderCost = 10d;
const double fixedTradeFee = 0.01d;
#endif

const double tradeFee = 0.002d; // 0.007
const double tradeRebate = 0; // 0.00025d;
#endregion

#if CMA
#if GA
Ga<CurrencyMathematicalAveraging, CurrencyMathematicalAveragingChromosome>();
#else
StaticTest<CurrencyMathematicalAveraging, CurrencyMathematicalAveragingChromosome>(x =>
{
    x.buyAgressivness.Replace(15d);
    x.sellAgressivness.Replace(2.5d);
});
#endif
#elif GAMMA
#if GA
Ga<GammaStrategy, GammaStrategyChromosome>();
#else
StaticTest<GammaStrategy, GammaStrategyChromosome>();
#endif
#elif LEVELS
#if GA
Ga<LevelsStrategy, LevelsStrategyChromosome>();
#else
StaticTest<LevelsStrategy, LevelsStrategyChromosome>();
#endif
#endif


void Ga<T,S>() 
    where T : IStrategyPrototype<S>, new()
    where S : SpreadChromosome
{
    // todo: downloader, chunks
    // todo: fitness max cost

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
    var population = new Population(250, 1250, strategy.GetAdamChromosome());
    var fitness = new DynamicFitness<S>(x => Evaluate<S>(prices, x.ToRequest(), strategy.CreateInstance(x), null));
    var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation)
    {
        Termination = termination,
        TaskExecutor = executor
    };

    var directory = GetDirectory(outputSuffix);

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

void StaticTest<T,S>(Action<S> tweak = null)
    where T : IStrategyPrototype<S>, new()
    where S : SpreadChromosome, new()
{
    var config = JsonSerializer.Deserialize<Config>(Encoding.UTF8.GetString(Convert.FromBase64String(File.ReadAllText(configFile).Trim('{', '}', ' '))));
    var chromosome = new S();
    chromosome.FromConfig(config);
    tweak?.Invoke(chromosome);

    var prices = File
        .ReadAllLines(filename)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => double.Parse(x.Split(',')[0], NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture))
        .ToList();
    using var writer = File.CreateText("out.csv");

    var sw = new Stopwatch();
    sw.Restart();
    Evaluate(prices, chromosome.ToRequest(), new T().CreateInstance(chromosome), writer);
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

    // init strategy if needed
    strategy.OnTrade(prices.First(), 0, 0, currency);

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
            strategy.OnTrade(price, asset, size, currency - budgetExtra);
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
