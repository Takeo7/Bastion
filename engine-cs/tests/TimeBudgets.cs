using System;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>Tests that measure time run in this collection, alone, after the rest (xUnit runs it without parallelism).</summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class TimeBudgets
    {
        public const string Name = "Time budgets";

        /// <summary>
        /// A budget, in milliseconds. They are those of our machines: on GitHub's (the variable CI), slower and shared,
        /// five times as much, which still catches what has gone badly wrong.
        /// </summary>
        public static double Ms(double ms)
        {
            return Environment.GetEnvironmentVariable("CI") == null ? ms : ms * 5;
        }
    }
}
