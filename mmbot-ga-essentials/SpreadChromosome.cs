using GeneticSharp.Domain.Chromosomes;
using GeneticSharp.Domain.Randomizations;
using MMBot.Api.dto;
using System.Text.Json;

namespace MMBotGA.ga
{
    public abstract class SpreadChromosome : ChromosomeBase
    {
        protected readonly GeneFactory Factory;

        protected SpreadChromosome(bool finalize) : base(2)
        {
            Factory = new GeneFactory(this);

            // max is exclusive
            Stdev = Factory.Create(() => RandomizationProvider.Current.GetDouble(1, 100));
            Sma = Stdev; //MCA
            Mult = Factory.Create(() => RandomizationProvider.Current.GetDouble(0.95, 1.05));
            Raise = Factory.Create(() => RandomizationProvider.Current.GetDouble(1, 1000));
            Fall = Factory.Create(() => RandomizationProvider.Current.GetDouble(0.1, 10));
            Cap = Factory.Create(() => RandomizationProvider.Current.GetDouble(0, 100));
            ModeGene = Factory.Create(() => RandomizationProvider.Current.GetInt(0, _modes.Length));
            DynMultGene = Factory.Create(() => RandomizationProvider.Current.GetInt(0, 2));
            FreezeGene = Factory.Create(() => RandomizationProvider.Current.GetInt(0, 2));

            //Static gene example:
            //Trend = _factory.Create(0d);

            if (finalize)
            {
                FinalizeGenes();
            }
        }

        protected void FinalizeGenes()
        {
            Resize(Factory.Length);
            CreateGenes();
        }

        #region Spread

        public GeneWrapper<double> Stdev { get; }
        public GeneWrapper<double> Sma { get; }
        public GeneWrapper<double> Mult { get; }
        public GeneWrapper<double> Raise { get; }
        public GeneWrapper<double> Fall { get; }
        public GeneWrapper<double> Cap { get; }

        private readonly string[] _modes = { "independent", "together", "alternate", "half_alternate" }; // "disabled"
        private GeneWrapper<int> ModeGene { get; }
        public string Mode => _modes[ModeGene.Value];

        private GeneWrapper<int> DynMultGene { get; }
        public bool DynMult => DynMultGene.Value == 1;

        private GeneWrapper<int> FreezeGene { get; }
        public bool Freeze => FreezeGene.Value == 1;

        #endregion

        public GenTradesRequest ToRequest()
        {
            return new GenTradesRequest
            {
                BeginTime = 0,
                Sma = Sma,
                Stdev = Stdev,
                ForceSpread = 0,
                Mult = Mult,
                Raise = Raise,
                Cap = Cap,
                Fall = Fall,
                Mode = Mode,
                Sliding = false,
                SpreadFreeze = Freeze,
                DynMult = DynMult,
                Reverse = false,
                Invert = false,
                Ifutures = false,
                // Order2 = 50
            };
        }

        public virtual Config ToConfig()
        {
            return new Config
            {
                Enabled = true,
                AdjTimeout = 5,

                SpreadCalcSmaHours = Sma,
                SpreadCalcStdevHours = Stdev,
                DynmultMode = Mode,
                DynmultSliding = false,
                SpreadFreeze = Freeze,
                DynmultMult = DynMult,
                DynmultRaise = Raise,
                DynmultFall = Fall,
                DynmultCap = Cap,
                SellStepMult = Mult,
                BuyStepMult = Mult,
                //SecondaryOrder = 50,
            };
        }

        public virtual void FromConfig(Config config)
        {
            Sma.Replace(config.SpreadCalcSmaHours);
            Stdev.Replace(config.SpreadCalcStdevHours);
            ModeGene.Replace(Array.IndexOf(_modes, config.DynmultMode));
            FreezeGene.Replace(config.SpreadFreeze ? 1 : 0);
            DynMultGene.Replace(config.DynmultMult ? 1 : 0);
            Raise.Replace(config.DynmultRaise);
            Fall.Replace(config.DynmultFall);
            Cap.Replace(config.DynmultCap);
            Mult.Replace(config.SellStepMult);
        }

        public override Gene GenerateGene(int geneIndex) => Factory.Generate(geneIndex);
    }
}
