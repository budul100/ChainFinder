using System;
using System.Collections.Generic;
using System.Linq;
using ChainFinder.Settings;
using ChainFinder.Tests.Data;
using ChainFinder.Tests.Legacy;
using Xunit;

namespace ChainFinder.Tests
{
    // Old converter logic vs. new Finder, compared as sets of edge id sequences
    public class FinderDifferentialTests(ITestOutputHelper output)
    {
        #region Private Fields

        private static readonly IReferenceChains Reference = new ReferenceChains();

        #endregion Private Fields

        #region Public Methods

        public static IEnumerable<object[]> NamedCombinations()
            => from named in GraphCases.Named()
               from flag in new[] { false, true }
               select new[] { named[0], named[1], flag };

        public static IEnumerable<object[]> SeedCombinations()
            => from seed in GraphCases.Seeds()
               from flag in new[] { false, true }
               select new[] { seed[0], flag };

        [Theory]
        [MemberData(nameof(NamedCombinations))]
        public void Named_graphs_match(string name, TestEdge[] edges, bool preventGoingTwice)
            => AssertSame(edges, preventGoingTwice, name);

        [Theory]
        [MemberData(nameof(SeedCombinations))]
        public void Random_graphs_match(int seed, bool preventGoingTwice)
            => AssertSame(GraphCases.Random(seed), preventGoingTwice, $"seed {seed}");

        #endregion Public Methods

        #region Private Methods

        // Order of chains is irrelevant, order within a chain is not
        private static string[] Normalize(IEnumerable<int[]> chains)
            => chains
                .Select(c => string.Join("-", c))
                .Distinct().OrderBy(k => k).ToArray();

        // Mirrors the converter call, preventGoingTwice maps to allowEdgeReuse (inverted)
        private static IReadOnlyCollection<int[]> RunFinder(TestEdge[] edges, bool preventGoingTwice)
        {
            var finder = new Finder<TestEdge, int>(
                elements: edges,
                getterKey: e => e.From,
                getterNext: e => e.To,
                predicateNext: (f, t) => !(f.From == t.To && f.To == t.From),
                allowEdgeReuse: !preventGoingTwice,
                preventKeyRevisit: true,
                coverAll: true,
                reduction: ReductionType.None);

            return finder.Find()
                .Select(c => c.Select(e => e.Id).ToArray()).ToArray();
        }

        private void AssertSame(TestEdge[] edges, bool preventGoingTwice, string label)
        {
            var expected = Normalize(Reference.Get(edges, preventGoingTwice));
            var actual = Normalize(RunFinder(edges, preventGoingTwice));

            if (!expected.SequenceEqual(actual))
            {
                output.WriteLine($"{label}, preventGoingTwice={preventGoingTwice}");
                output.WriteLine("Edges:    " + string.Join(" ", edges.Select(e => $"{e.Id}:{e.From}>{e.To}")));
                output.WriteLine("Old only: " + string.Join(" | ", expected.Except(actual)));
                output.WriteLine("New only: " + string.Join(" | ", actual.Except(expected)));
            }

            Assert.Equal(expected, actual);
        }

        #endregion Private Methods
    }
}