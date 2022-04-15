internal class VisSpread : IVisSpread
{
    public record Config(
        DynMultControl.Config Dynmult,
        double Mult = 1,
        double Order2 = 0,
        bool Sliding = false,
        bool Freeze = false
    );

    public VisSpread(ISpreadFunction fn, Config cfg)
    {
        _fn = fn;
        _state = fn.Start();
        _dynmult = new DynMultControl(cfg.Dynmult);
        _sliding = cfg.Sliding;
        _freeze = cfg.Freeze;
        _mult = cfg.Mult;
        _order2 = cfg.Order2 * 0.01;
    }

    public IVisSpread.Result Point(double y)
    {
        var sp = _fn.Point(_state, y);
        if (_lastPrice == 0)
        {
            _lastPrice = y;
            _offset = y;
            return new IVisSpread.Result();
        }
        if (!sp.Valid) return new IVisSpread.Result();

        var trade = 0;
        var trade2 = 0;
        var price = _lastPrice;
        double price2 = 0;

        var center = _sliding ? sp.Center : 0;
        if (y > _chigh)
        {
            var high2 = _chigh.Value * Math.Exp(_cspread * _order2);
            price = _chigh.Value;
            _lastPrice = _chigh.Value;
            _offset = _chigh.Value - center;
            trade = -1;
            if (_order2 != 0 && y > high2)
            {
                trade2 = -1;
                price2 = high2;
                _offset = high2 - center;
                _lastPrice = high2;
            }
            _dynmult.Update(false, true);
            /*if (frozen_side != -1)*/
            {
                _frozenSide = -1;
                _frozenSpread = _cspread;
            }
        }
        else if (y < _clow)
        {
            var low2 = _clow.Value * Math.Exp(-_cspread * _order2);
            price = _clow.Value;
            _lastPrice = _clow.Value;
            _offset = _clow.Value - center;
            trade = 1;
            if (_order2 != 0 && y < low2)
            {
                _lastPrice = low2;
                trade2 = 1;
                _offset = low2 - center;
                price2 = low2;
            }
            _dynmult.Update(true, false);
            /*if (frozen_side != 1)*/
            {
                _frozenSide = 1;
                _frozenSpread = _cspread;
            }
        }
        _dynmult.Update(false, false);

        var lspread = sp.Spread;
        var hspread = sp.Spread;
        if (_freeze)
        {
            if (_frozenSide < 0)
            {
                lspread = Math.Min(_frozenSpread, lspread);
            }
            else if (_frozenSide > 0)
            {
                hspread = Math.Min(_frozenSpread, hspread);
            }
        }
        var low = (center + _offset) * Math.Exp(-lspread * _mult * _dynmult.GetBuyMult());
        var high = (center + _offset) * Math.Exp(hspread * _mult * _dynmult.GetSellMult());
        if (_sliding && _lastPrice != 0)
        {
            var lowMax = _lastPrice * Math.Exp(-lspread * 0.01);
            var highMin = _lastPrice * Math.Exp(hspread * 0.01);
            if (low > lowMax)
            {
                high = lowMax + (high - low);
                low = lowMax;
            }
            if (high < highMin)
            {
                low = highMin - (high - low);
                high = highMin;

            }
            low = Math.Min(lowMax, low);
            high = Math.Max(highMin, high);
        }
        low = Math.Min(low, y);
        high = Math.Max(high, y);
        _chigh = high;
        _clow = low;
        _cspread = sp.Spread;
        return new IVisSpread.Result(true, price, low, high, trade, price2, trade2);
    }

    public IVisSpread.Result Point(double y, IStrategyCallback strategy)
    {
        var sp = _fn.Point(_state, y);
        if (_lastPrice == 0)
        {
            _lastPrice = y;
            _offset = y;
            return new IVisSpread.Result();
        }
        if (!sp.Valid) return new IVisSpread.Result();

        var trade = 0;
        var trade2 = 0;
        var price = _lastPrice;
        double price2 = 0;

        if (_sliding) throw new NotSupportedException("Sliding spread is not supported.");

        var center = strategy.GetCenterPrice(price);
        if (y > _chigh)
        {
            price = _chigh.Value;
            _lastPrice = _chigh.Value;
            _offset = _chigh.Value - center;
            trade = -1;
            _dynmult.Update(false, true);
            /*if (frozen_side != -1)*/
            {
                _frozenSide = -1;
                _frozenSpread = _cspread;
            }
        }
        else if (y < _clow)
        { 
            price = _clow.Value;
            _lastPrice = _clow.Value;
            _offset = _clow.Value - center;
            trade = 1;
            _dynmult.Update(true, false);
            /*if (frozen_side != 1)*/
            {
                _frozenSide = 1;
                _frozenSpread = _cspread;
            }
        }
        _dynmult.Update(false, false);

        var lspread = sp.Spread;
        var hspread = sp.Spread;
        if (_freeze)
        {
            if (_frozenSide < 0)
            {
                lspread = Math.Min(_frozenSpread, lspread);
            }
            else if (_frozenSide > 0)
            {
                hspread = Math.Min(_frozenSpread, hspread);
            }
        }

        //ln 990
        //replace below with logic from mmbot
        var low = CalculateOrderFeeLess(strategy, center, y, -lspread * _mult, _dynmult.GetBuyMult());
        var high = CalculateOrderFeeLess(strategy, center, y, hspread * _mult, _dynmult.GetSellMult());

        //var low = (center + _offset) * Math.Exp(-lspread * _mult * _dynmult.GetBuyMult()); //lspread * mult = step, mult = dynmult
        //var high = (center + _offset) * Math.Exp(hspread * _mult * _dynmult.GetSellMult());
        //if (_sliding && _lastPrice != 0)
        //{
        //    var lowMax = _lastPrice * Math.Exp(-lspread * 0.01);
        //    var highMin = _lastPrice * Math.Exp(hspread * 0.01);
        //    if (low > lowMax)
        //    {
        //        high = lowMax + (high - low);
        //        low = lowMax;
        //    }
        //    if (high < highMin)
        //    {
        //        low = highMin - (high - low);
        //        high = highMin;

        //    }
        //    low = Math.Min(lowMax, low);
        //    high = Math.Max(highMin, high);
        //}
        low = Math.Min(low, y);
        high = Math.Max(high, y);
        _chigh = high;
        _clow = low;
        _cspread = sp.Spread;
        return new IVisSpread.Result(true, price, low, high, trade, price2, trade2);
    }

    private static double AdjustSize(double size, int dir)
    {
        if (size * dir < 0 || size == 0)
        {
            return 0;
        }

        // todo: max/min size, within budget ... 

        return size;
    }

    private static double CalculateOrderFeeLess(IStrategyCallback state, double prevPrice, double curPrice, double step, double dynmult)
    {
        var m = 1d;
        if (double.IsNaN(step)) step = 0;
        var dir = Math.Sign(-step);

        var newPrice = prevPrice * Math.Exp(step * dynmult * m);
        if ((newPrice - curPrice) * dir > 0 || double.IsInfinity(newPrice) || newPrice <= 0)
        {
            newPrice = curPrice;
            prevPrice = newPrice / Math.Exp(step * dynmult * m);
        }

        var cnt = 0;
        var sz = 0d;
        double prevSz;
        do
        {
            prevSz = sz;
            newPrice = prevPrice * Math.Exp(step * dynmult * m);

            if ((newPrice - curPrice) * dir > 0)
            {
                newPrice = curPrice;
            }

            sz = AdjustSize(state.GetSize(newPrice, dir), dir);

            cnt++;
            m *= 1.1d;
        } while (cnt < 1000 && sz == 0 && ((sz - prevSz) * dir > 0 || cnt < 10));

        return newPrice;
    }


    private ISpreadFunction _fn;
    private object _state;
    private DynMultControl _dynmult;
    private bool _sliding;
    private bool _freeze;
    private double _mult;
    private double _order2;
    private double _offset = 0;
    private double _lastPrice = 0;
    private double? _chigh, _clow;
    private double _cspread;
    private int _frozenSide = 0;
    private double _frozenSpread = 0;
}