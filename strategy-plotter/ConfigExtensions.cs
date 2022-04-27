using MMBot.Api.dto;
using System.Text.Json;

namespace strategy_plotter
{
    public static class ConfigExtensions
    {
        public static T ParseStrategyConfig<T>(this Config config, string typeAssertion)
            where T : IStrategyConfig
        {
            if (config.Strategy is not T s)
            {
                s = JsonSerializer.Deserialize<T>(config.Strategy.ToString());
            }

            if (s.Type != typeAssertion)
            {
                throw new ArgumentException($"Loaded configuration does not belong to strategy {typeAssertion}, but {s.Type} instead.");
            }

            return s;
        }
    }
}