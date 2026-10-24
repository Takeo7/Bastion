using System.Diagnostics;

namespace Bastion.Net
{
    /// <summary>
    /// Milliseconds that only go forward (never the wall clock, which jumps when it is set). The room and the client take
    /// one, so their grace periods, waits and turn clock run on a fake one in the tests, without waiting for real.
    /// </summary>
    public interface IClock
    {
        long Now { get; }
    }

    /// <summary>The real one.</summary>
    public sealed class SystemClock : IClock
    {
        private readonly Stopwatch watch = Stopwatch.StartNew();

        public long Now => watch.ElapsedMilliseconds;
    }
}
