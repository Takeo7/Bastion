using System.IO;
using System.Linq;
using System.Text.Json;
using Bastion.Engine;
using Xunit;

namespace Bastion.Engine.Tests
{
    public class RngTests
    {
        private static readonly JsonElement Golden = JsonDocument.Parse(File.ReadAllText(Path.Combine("golden", "rng.json"))).RootElement;

        [Theory]
        [InlineData(0u)]
        [InlineData(1u)]
        [InlineData(7u)]
        [InlineData(42u)]
        [InlineData(123456u)]
        [InlineData(4294967295u)]
        public void TheSameNumbersAsRngTsSeedForSeed(uint seed)
        {
            var g = Golden.GetProperty(seed.ToString());
            var r = new Rng(seed);
            Assert.Equal(g.GetProperty("next").EnumerateArray().Select((x) => x.GetDouble()), Enumerable.Range(0, 8).Select((_) => r.Next()));
            Assert.Equal(g.GetProperty("ints").EnumerateArray().Select((x) => x.GetInt32()), Enumerable.Range(0, 8).Select((_) => r.Int(100)));
            Assert.Equal(g.GetProperty("state").EnumerateArray().Select((x) => x.GetInt64()), r.State);
        }

        [Fact]
        public void FromASavedStateWithNegativeNumbers()
        {
            var r = new Rng(new long[] { -1, 2, -3, 4 });
            Assert.Equal(Golden.GetProperty("state").GetProperty("next").EnumerateArray().Select((x) => x.GetDouble()), Enumerable.Range(0, 4).Select((_) => r.Next()));
        }
    }
}
