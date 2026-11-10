using System;
using Xunit;

namespace Bastion.Engine.Tests
{
    /// <summary>
    /// The sieges are out of the campaign for now (Arturo, 7/11: Campaign.SiegesOn), and the rules under them go on
    /// changing: their tests go on running, each one with the sieges switched on for itself (WithSieges). The switch is
    /// one for the whole engine, so they run in this collection, alone, after the rest (xUnit runs it without
    /// parallelism), as the time budgets do.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class SiegesSwitch
    {
        public const string Name = "Sieges on";
    }

    /// <summary>A class of siege tests: xUnit makes one of it for each test, and disposes of it after.</summary>
    public abstract class WithSieges : IDisposable
    {
        protected WithSieges()
        {
            Campaign.SiegesOn = true;
        }

        public void Dispose()
        {
            Campaign.SiegesOn = false;
        }
    }
}
