using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>Tests that measure time run in this collection, alone, after the rest (xUnit runs it without parallelism).</summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class TimeBudgets
    {
        public const string Name = "Time budgets";
    }
}
