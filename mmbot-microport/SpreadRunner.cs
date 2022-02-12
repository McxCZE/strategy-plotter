using MMBot.Api.dto;
using mmbot_microport.utils;

public static class SpreadRunner
{
    public static ICollection<BTPrice> GenerateTrades(GenTradesRequest request, ICollection<double> srcMinute)
    {
        var fn = new DefaulSpread(request.Sma, request.Stdev, request.ForceSpread);
        var spreadCalc = new VisSpread(fn, new VisSpread.Config(
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
        if (request.Reverse)
        {
            srcMinute = new List<double>(srcMinute);
            srcMinute.Reverse();
        }

        var t = request.BeginTime ?? UnixEpoch.GetEpochMs(DateTime.UtcNow.AddMinutes(-srcMinute.Count));
        var ofs = request.Offset ?? 0;
        var lim = Math.Min(request.Limit ?? srcMinute.Count, srcMinute.Count - ofs);

        var result = new List<BTPrice>();
        BTPrice last = null;
        var init = 0d;
        foreach (var itm in srcMinute.Skip(ofs).Take(lim))
        {
            var w = itm;
            if (request.Invert)
            {
                if (init == 0) init = w * w;
                w = init / w;
            }

            var v = w;
            if (request.Ifutures)
            {
                v = 1 / v;
            }

            var res = spreadCalc.Point(v);
            if (res.Trade != 0 && res.Valid)
            {
                var p = request.Ifutures ? 1.0 / res.Price : res.Price;
                result.Add(last = new BTPrice(t, p, p, p));
                if (res.Trade2 != 0)
                {
                    p = request.Ifutures ? 1.0 / res.Price2 : res.Price2;
                    result.Add(last = new BTPrice(t, p, p, p));
                }
            }
            else if (w < last?.Pmin)
            {
                last = last with
                {
                    Pmin = w
                };
            }
            else if (w > last?.Pmax)
            {
                last = last with
                {
                    Pmax = w
                };
            }

            t += 60000;
        }

        return result;
    }
}