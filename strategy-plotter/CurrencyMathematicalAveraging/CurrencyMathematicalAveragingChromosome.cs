using GeneticSharp.Domain.Chromosomes;
using GeneticSharp.Domain.Randomizations;
using MMBot.Api.dto;
using MMBotGA.ga;
using System.Text.Json;

namespace strategy_plotter.CurrencyMathematicalAveraging
{
    class CurrencyMathematicalAveragingChromosome : SpreadChromosome
    {
        public CurrencyMathematicalAveragingChromosome() : base(false)
        {
            buyAgressivness = Factory.Create(() => RandomizationProvider.Current.GetDouble(0.01, 100)); //0-1
            sellAgressivness = Factory.Create(() => RandomizationProvider.Current.GetDouble(0.01, 100)); //0-1
            FinalizeGenes();
        }

        public GeneWrapper<double> buyAgressivness { get; }
        public GeneWrapper<double> sellAgressivness { get; }

        public override IChromosome CreateNew() => new CurrencyMathematicalAveragingChromosome();

        public override Config ToConfig()
        {
            var res = base.ToConfig();
            res.Strategy = new CurrencyMathematicalAveragingConfig
            {
                Type = "cma",
                buyAgressivness = buyAgressivness,
                sellAggressivness = sellAgressivness,
                Backtest = false
            };
            return res;
        }

        public override void FromConfig(Config config)
        {
            base.FromConfig(config);

            if (config.Strategy is not CurrencyMathematicalAveragingConfig s)
            {
                s = JsonSerializer.Deserialize<CurrencyMathematicalAveragingConfig>(config.Strategy.ToString());
            }

            buyAgressivness.Replace(s.buyAgressivness);
            sellAgressivness.Replace(s.sellAggressivness);
        }
    }
}