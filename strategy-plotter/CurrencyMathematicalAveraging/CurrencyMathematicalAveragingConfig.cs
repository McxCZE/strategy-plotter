using System.Text.Json.Serialization;

namespace strategy_plotter.CurrencyMathematicalAveraging
{
    public class CurrencyMathematicalAveragingConfig : IStrategyConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("alpha")]
        public double alpha { get; set; }

        [JsonPropertyName("bravo")]
        public double bravo { get; set; }

        [JsonPropertyName("charlie")]
        public double charlie { get; set; }

        [JsonPropertyName("backtest")]
        public bool Backtest { get; set; }
    }
}