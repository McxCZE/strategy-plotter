using System.Text.Json.Serialization;

namespace strategy_plotter.CurrencyMathematicalAveraging
{
    public class CurrencyMathematicalAveragingConfig : IStrategyConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("buyAggressivness")]
        public double buyAgressivness { get; set; }

        [JsonPropertyName("sellAggressivness")]
        public double sellAggressivness { get; set; }

        [JsonPropertyName("backtest")]
        public bool Backtest { get; set; }
    }
}