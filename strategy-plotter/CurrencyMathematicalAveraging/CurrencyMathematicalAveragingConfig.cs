using System.Text.Json.Serialization;

namespace strategy_plotter.CurrencyMathematicalAveraging
{
    public class CurrencyMathematicalAveragingConfig : IStrategyConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("buyStrength")]
        public double BuyStrength { get; set; }

        [JsonPropertyName("sellStrength")]
        public double SellStrength { get; set; }

        [JsonPropertyName("initBet")]
        public double InitBet { get; set; }

        [JsonPropertyName("backtest")]
        public bool Backtest { get; set; }
    }
}