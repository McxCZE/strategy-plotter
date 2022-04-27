using System.Text.Json.Serialization;

namespace strategy_plotter.CurrencyMathematicalAveraging
{
    public class CurrencyMathematicalAveragingConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("dumbDcaAgressivness")]
        public double DumbDcaAgressivness { get; set; }

        [JsonPropertyName("backtest")]
        public bool Backtest { get; set; }
    }
}