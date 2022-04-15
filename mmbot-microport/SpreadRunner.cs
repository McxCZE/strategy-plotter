using MMBot.Api.dto;
using mmbot_microport.utils;

public class Simulator
{
    private readonly IVisSpread _spread;
    private readonly IStrategyCallback _strategy;
    private readonly GenTradesRequest _request;

    private long _time = 0;
    private double _init = 0d;

    public Simulator(GenTradesRequest request, IStrategyCallback strategy, long time)
    {
        _time = time;
        _spread = SpreadRunner.GetSpread(request);
        _strategy = strategy;
        _request = request;
    }

    public SpreadRunner.MetaTrade Tick(double price, bool warmup)
    {
        var w = price;
        if (_request.Invert)
        {
            if (_init == 0) _init = w * w;
            w = _init / w;
        }

        var v = w;
        if (_request.Ifutures)
        {
            v = 1 / v;
        }

        if (warmup)
        {
            _spread.Point(v);
            return null;
        }

        var meta = new SpreadRunner.MetaSpread(w, _time, _spread.Point(v, _strategy));

        _time += 60000;

        if (meta.Spread.Trade != 0 && meta.Spread.Valid)
        {
            var p = _request.Ifutures ? 1.0 / meta.Price : meta.Price;

            if (meta.Spread.Trade2 != 0) throw new NotSupportedException("Secondary order is not supported.");

            return new SpreadRunner.MetaTrade(meta, new BTPrice(meta.Time, p, p, p));
        }

        return new SpreadRunner.MetaTrade(meta, null);
    }
}

public interface IStrategyCallback
{
    double GetSize(double price, double dir);
    double GetCenterPrice(double price);
}

public class DynamicStrategyCallback : IStrategyCallback
{
    private readonly Func<double, double, double> getSize;
    private readonly Func<double, double> getCenterPrice;

    public DynamicStrategyCallback(Func<double, double, double> getSize, Func<double, double> getCenterPrice)
    {
        this.getSize = getSize;
        this.getCenterPrice = getCenterPrice;
    }

    public double GetCenterPrice(double price)
    {
        return getCenterPrice(price);
    }

    public double GetSize(double price, double dir)
    {
        return getSize(price, dir);
    }
}

public static class SpreadRunner
{
    public static IVisSpread GetSpread(GenTradesRequest request)
    {
        var fn = new DefaulSpread(request.Sma, request.Stdev, request.ForceSpread);
        return new VisSpread(fn, new VisSpread.Config(
            new DynMultControl.Config(
                request.Raise,
                request.Fall,
                request.Cap,
                Enum.Parse<DynmultModeType>(request.Mode, true),
                request.DynMult
            ),
            request.Mult,
            request.Order2,
            request.Sliding,
            request.SpreadFreeze
        ));
    }

    public record MetaSpread(double Price, long Time, IVisSpread.Result Spread);

    public static IEnumerable<MetaTrade> GenerateTrades(GenTradesRequest request, ICollection<double> srcMinute)
    {
        if (request.Reverse)
        {
            var rev = new List<double>(srcMinute);
            rev.Reverse();
            srcMinute = rev;
        }

        var t = request.BeginTime ?? UnixEpoch.GetEpochMs(DateTime.UtcNow.AddMinutes(-srcMinute.Count));
        var ofs = request.Offset ?? 0;
        var lim = Math.Min(request.Limit ?? srcMinute.Count, srcMinute.Count - ofs);
        var simulator = new Simulator(request, null, t);
        foreach (var itm in srcMinute.Skip(ofs).Take(lim))
        {
            yield return simulator.Tick(itm, false);
        }
    }

    public record MetaTrade(MetaSpread Spread, BTPrice trade);
}