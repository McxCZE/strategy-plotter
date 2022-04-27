using GeneticSharp.Domain.Chromosomes;
using MMBot.Api.dto;
using MMBotGA.ga;

namespace strategy_plotter.Levels
{
    class LevelsStrategyChromosome : SpreadChromosome
    {
        public LevelsStrategyChromosome() : base(false)
        {
            //InitialBetPercOfBudget = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 1)); //0-1

            FinalizeGenes();
        }

        public override IChromosome CreateNew() => new LevelsStrategyChromosome();

        public override Config ToConfig()
        {
            var res = base.ToConfig();
            res.Strategy = new LevelsStrategyConfig
            {
                Type = "levels"
            };
            return res;
        }

        public override void FromConfig(Config config)
        {
            base.FromConfig(config);

            var s = config.ParseStrategyConfig<LevelsStrategyConfig>("levels");
        }
    }
}