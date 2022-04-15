public interface IVisSpread
{
    Result Point(double y);

    Result Point(double y, IStrategyCallback strategy);

    public record Result(
    bool Valid = false,
    double Price = 0,
    double Low = 0,
    double High = 0,
    int Trade = 0, //0=no trade, -1=sell, 1=buy
    double Price2 = 0, //price of secondary trade
    int Trade2 = 0  //0 = no secondary trade, -1=sell, 1=buy
);
}