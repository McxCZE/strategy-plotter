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
            Alpha = Factory.Create(() => RandomizationProvider.Current.GetDouble(0.1, 1)); //0.15-0.55
            Bravo = Factory.Create(() => RandomizationProvider.Current.GetDouble(0.1, 50)); //5-30
            Charlie = Factory.Create(() => RandomizationProvider.Current.GetDouble(0.1, 50)); //6.5-10
            Delta = Factory.Create(() => RandomizationProvider.Current.GetDouble(0.1, 10)); //6.5-10
            FinalizeGenes();
        }

        public GeneWrapper<double> Alpha { get; }
        public GeneWrapper<double> Bravo { get; }
        public GeneWrapper<double> Charlie { get; }
        public GeneWrapper<double> Delta { get; }

        public override IChromosome CreateNew() => new CurrencyMathematicalAveragingChromosome();

        public override Config ToConfig()
        {
            var res = base.ToConfig();
            res.Strategy = new CurrencyMathematicalAveragingConfig
            {
                Type = "cma",
                Alpha = Alpha,
                Bravo = Bravo,
                Charlie = Charlie,
                Delta = Delta,
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

            Alpha.Replace(s.Alpha);
            Bravo.Replace(s.Bravo);
            Charlie.Replace(s.Charlie);
            Delta.Replace(s.Delta);
        }
    }
}