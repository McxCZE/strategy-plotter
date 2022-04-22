using GeneticSharp.Domain.Chromosomes;
using GeneticSharp.Domain.Randomizations;
using MMBot.Api.dto;
using MMBotGA.ga;
using System.Text.Json;

namespace strategy_plotter.Epa
{
    class EnterPriceAngleStrategyChromosome : SpreadChromosome
    {
        public EnterPriceAngleStrategyChromosome() : base(false)
        {
            InitialBetPercOfBudget = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 1)); //0-1

            MaxEnterPriceDistance = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 0.5));
            PowerMult = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 10));
            PowerCap = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 10));

            Angle = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 70)); //0-90

            TargetExitPriceDistance = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 0.5));
            ExitPowerMult = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 10));

            ReductionMidpoint = Factory.Create(() => RandomizationProvider.Current.GetDouble(0.1, 0.9)); //Factory.Create(0.6d);

            DipRescuePercOfBudget = Factory.Create(0d); //0.5 - 1
            DipRescueEnterPriceDistance = Factory.Create(0.2d); //0.2 - 2

            FinalizeGenes();
        }

        public GeneWrapper<double> InitialBetPercOfBudget { get; }

        public GeneWrapper<double> MaxEnterPriceDistance { get; }
        public GeneWrapper<double> PowerMult { get; }
        public GeneWrapper<double> PowerCap { get; }

        public GeneWrapper<double> Angle { get; }

        public GeneWrapper<double> TargetExitPriceDistance { get; }
        public GeneWrapper<double> ExitPowerMult { get; }

        public GeneWrapper<double> ReductionMidpoint { get; }

        public GeneWrapper<double> DipRescuePercOfBudget { get; }
        public GeneWrapper<double> DipRescueEnterPriceDistance { get; }

        public override IChromosome CreateNew() => new EnterPriceAngleStrategyChromosome();

        public override Config ToConfig()
        {
            var res = base.ToConfig();
            res.Strategy = new EpaStrategyConfig
            {
                Type = "enter_price_angle",
                Angle = Angle,
                ExitPowerMult = ExitPowerMult,
                InitialBetPercOfBudget = InitialBetPercOfBudget,
                MaxEnterPriceDistance = MaxEnterPriceDistance,
                MinAssetPercOfBudget = 0.001,
                PowerCap = PowerCap,
                PowerMult = PowerMult,
                TargetExitPriceDistance = TargetExitPriceDistance,
                ReductionMidpoint = ReductionMidpoint,
                DipRescuePercOfBudget = DipRescuePercOfBudget,
                DipRescueEnterPriceDistance = DipRescueEnterPriceDistance,
                Backtest = false
            };
            return res;
        }

        public override void FromConfig(Config config)
        {
            base.FromConfig(config);

            if (config.Strategy is not EpaStrategyConfig s)
            {
                s = JsonSerializer.Deserialize<EpaStrategyConfig>(config.Strategy.ToString());
            }

            Angle.Replace(s.Angle);
            ExitPowerMult.Replace(s.ExitPowerMult);
            InitialBetPercOfBudget.Replace(s.InitialBetPercOfBudget);
            MaxEnterPriceDistance.Replace(s.MaxEnterPriceDistance);
            PowerCap.Replace(s.PowerCap);
            PowerMult.Replace(s.PowerMult);
            TargetExitPriceDistance.Replace(s.TargetExitPriceDistance);
            ReductionMidpoint.Replace(s.ReductionMidpoint);
            DipRescuePercOfBudget.Replace(s.DipRescuePercOfBudget);
            DipRescueEnterPriceDistance.Replace(s.DipRescueEnterPriceDistance);
        }
    }
}