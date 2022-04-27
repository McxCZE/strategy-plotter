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
            DumbDcaAgressivness = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 1)); //0-1

            FinalizeGenes();
        }

        public GeneWrapper<double> DumbDcaAgressivness { get; }


        public override IChromosome CreateNew() => new CurrencyMathematicalAveragingChromosome();

        public override Config ToConfig()
        {
            var res = base.ToConfig();
            res.Strategy = new CurrencyMathematicalAveragingConfig
            {
                Type = "enter_price_angle",
                DumbDcaAgressivness = DumbDcaAgressivness,
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

            DumbDcaAgressivness.Replace(s.DumbDcaAgressivness);
        }
    }
}