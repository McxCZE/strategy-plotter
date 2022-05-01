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
            BuyStrength = Factory.Create(() => RandomizationProvider.Current.GetDouble(0.001, 1)); //max:1
            SellStrength = Factory.Create(() => RandomizationProvider.Current.GetDouble(0.001, 1)); //max:1
            FinalizeGenes();
        }

        public GeneWrapper<double> BuyStrength { get; }

        public GeneWrapper<double> SellStrength { get; }

        public override IChromosome CreateNew() => new CurrencyMathematicalAveragingChromosome();

        public override Config ToConfig()
        {
            var res = base.ToConfig();
            res.Strategy = new CurrencyMathematicalAveragingConfig
            {
                Type = "cma",
                BuyStrength = BuyStrength,
                SellStrength = SellStrength,
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

            BuyStrength.Replace(s.BuyStrength);
            SellStrength.Replace(s.SellStrength);
        }
    }
}