using System.Text.Json.Serialization;

namespace strategy_plotter.Levels
{
    public class LevelsStrategyConfig : IStrategyConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }
}