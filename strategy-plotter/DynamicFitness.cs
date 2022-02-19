using GeneticSharp.Domain.Chromosomes;
using GeneticSharp.Domain.Fitnesses;

class DynamicFitness<T> : IFitness
    where T : class, IChromosome
{
    private readonly Func<T, double> _func;

    public DynamicFitness(Func<T, double> func)
    {
        _func = func;
    }

    public double Evaluate(IChromosome chromosome)
    {
        return _func(chromosome as T);
    }
}
