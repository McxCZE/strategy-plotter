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
            alpha = Factory.Create(() => RandomizationProvider.Current.GetDouble(0.15, 0.55)); //0.15-0.55
            bravo = Factory.Create(() => RandomizationProvider.Current.GetDouble(5, 30)); //5-30
            charlie = Factory.Create(() => RandomizationProvider.Current.GetDouble(6.5, 10)); //6.5-10
            FinalizeGenes();
        }

        public GeneWrapper<double> alpha { get; }
        public GeneWrapper<double> bravo { get; }
        public GeneWrapper<double> charlie { get; }

        public override IChromosome CreateNew() => new CurrencyMathematicalAveragingChromosome();

        public override Config ToConfig()
        {
            var res = base.ToConfig();
            res.Strategy = new CurrencyMathematicalAveragingConfig
            {
                Type = "cma",
                alpha = alpha,
                bravo = bravo,
                charlie = charlie,
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

            alpha.Replace(s.alpha);
            bravo.Replace(s.bravo);
            charlie.Replace(s.charlie);
        }
    }
}