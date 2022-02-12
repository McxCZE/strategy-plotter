using System.Globalization;

var random = new Random();

var filename = "FTX_DOGE-PERP_03.12.2020_03.12.2021.csv";
//var filename = "FTX_DOGE-PERP_03.12.2021_01.02.2022.csv";

var prices = File
    .ReadAllLines(filename)
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .Select(x => double.Parse(x, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture))
    .ToList();
using var writer = File.CreateText("out.csv");

var trades = SpreadRunner.GenerateTrades(new MMBot.Api.dto.GenTradesRequest
{
    BeginTime = 0,
    Stdev = 95,
    Sma = 2,
    Mult = 1,
    Mode = "Together",
    Raise = 113,
    Fall = 1.6,
    Cap = 58,
    DynMult = true
}, prices).ToDictionary(x => x.Time / 60000, x => x.Price);

writer.WriteLine("price,trade,size,cost,asset,currency,equity,enter");

var strategy = new Test();
var ep = 0d;
var asset = 0d;
const double budget = 10000d;
var currency = budget;
var index = 0;

foreach (var p in prices)
{
    var size = 0d;
    var price = p;
    var trade = trades.TryGetValue(index, out var genPrice);
    if (trade)
    {
        price = genPrice;
        size = strategy.GetSize(price, asset, budget, currency);
    }

    index++;
    var cost = price * size;
    if (cost > currency)
    {
        cost = currency;
        size = cost / price;
        currency = 0;
    }
    else if (-size > asset)
    {
        size = -asset;
        cost = price * size;
        currency -= cost;
    }
    else
    {
        currency -= cost;
    }

    if (size != 0)
    {
        strategy.OnTrade(price, asset, size);
    }

    var newAsset = asset + size;
    ep = size >= 0 ? ep + cost : (ep / asset) * newAsset;
    asset = newAsset;

    writer.Add(price);
    writer.Add(trade ? price.Ts() : string.Empty);
    writer.Add(size);
    writer.Add(cost);
    writer.Add(asset);
    writer.Add(currency);
    writer.Add(currency + (asset * price));
    writer.Add(asset == 0 ? 0 : ep / asset, true);
}

class Test
{
    double _ep = 0d;
    double _enter = double.NaN;    

    public double GetSize(double price, double asset, double budget, double currency)
    {
        var size = 0d;
        if (double.IsNaN(_enter) || (asset * price) < budget * 0.001)
        {
            // initial bet -> buy
            size = budget * 0.01;
        }
        else if (price < _enter)
        {
            // buy to lower enter price
            var dist = (_enter - price) / _enter;
            if (dist > 0.01)
            {
                size = asset * dist * 0.35;
            }
        }
        else
        {
            // sell?
            var dist = (price - _enter) / _enter;
            if (dist > 0.03)
            {
                size = -asset * dist * 0.9;
            }
        }

        return size;
    }

    public void OnTrade(double price, double asset, double size)
    {
        var newAsset = asset + size;
        _ep = size >= 0 ? _ep + (price * size) : (_ep / asset) * newAsset;
        _enter = _ep / newAsset;
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
