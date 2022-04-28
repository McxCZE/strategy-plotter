using System.Text.Json.Serialization;

namespace strategy_plotter.CurrencyMathematicalAveraging
{
    public class CurrencyMathematicalAveragingConfig : IStrategyConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("alpha")]
        public double Alpha { get; set; }

        [JsonPropertyName("bravo")]
        public double Bravo { get; set; }

        [JsonPropertyName("charlie")]
        public double Charlie { get; set; }

        [JsonPropertyName("delta")]
        public double Delta { get; set; }

        [JsonPropertyName("backtest")]
        public bool Backtest { get; set; }
    }
}