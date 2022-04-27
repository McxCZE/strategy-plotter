using GeneticSharp.Domain.Chromosomes;
using GeneticSharp.Domain.Randomizations;
using MMBot.Api.dto;
using MMBotGA.ga;

namespace strategy_plotter.Gamma
{
    public class GammaStrategyChromosome : SpreadChromosome
    {
        public GammaStrategyChromosome() : base(false)
        {
            // max is exclusive
            Exponent = Factory.Create(() => RandomizationProvider.Current.GetDouble(1, 20));
            Trend = Factory.Create(() => RandomizationProvider.Current.GetDouble(-110, 110));
            Rebalance = Factory.Create(() => RandomizationProvider.Current.GetInt(3, 5)); // always/smart
            //FunctionGene = Factory.Create(() => RandomizationProvider.Current.GetInt(0, _functions.Length));

            //Static gene example:
            //Trend = _factory.Create(0d);
            FunctionGene = Factory.Create(3);

            FinalizeGenes();
        }

        public GeneWrapper<double> Exponent { get; }

        public GeneWrapper<double> Trend { get; }
        public GeneWrapper<int> Rebalance { get; }

        private readonly string[] _functions = { "halfhalf", "keepvalue", "gauss", "exponencial" }; //"invsqrtsinh"
        private GeneWrapper<int> FunctionGene { get; }
        public string Function => _functions[FunctionGene.Value];

        public override IChromosome CreateNew() => new GammaStrategyChromosome();

        public override Config ToConfig()
        {
            var res = base.ToConfig();
            res.Strategy = new GammaStrategyConfig
            {
                Type = "gamma",
                Function = Function,
                Trend = Trend,
                Reinvest = false,

                Exponent = Exponent,
                Rebalance = Rebalance.ToString()
            };
            return res;
        }

        public override void FromConfig(Config config)
        {
            base.FromConfig(config);

            var s = config.ParseStrategyConfig<GammaStrategyConfig>("gamma");

            Exponent.Replace(s.Exponent);
            Trend.Replace(s.Trend);
            Rebalance.Replace(int.Parse(s.Rebalance));
            FunctionGene.Replace(Array.IndexOf(_functions, s.Function));
        }
    }
}