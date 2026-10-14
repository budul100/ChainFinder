using System.Collections.Generic;
using System.Linq;
using ChainFinder.Settings;
using ChainFinder.Tests.Data;
using Xunit;

namespace ChainFinder.Tests
{
    // Properties of the new Finder, independent of the old extension
    public class FinderTests
    {
        #region Public Methods

        [Fact]
        public void Branching_diamond_reaches_the_trunk_twice_when_edges_are_reused()
        {
            var keys = Keys(Run(GraphCases.BranchingDiamond(), allowEdgeReuse: true));

            Assert.Equal(new[] { "1-2-4-6", "1-3-5-6" }, keys);
        }

        [Fact]
        public void Branching_diamond_uses_the_trunk_once_when_edges_are_not_reused()
        {
            var keys = Keys(Run(GraphCases.BranchingDiamond(), allowEdgeReuse: false));

            Assert.Equal(new[] { "1-2-4-6", "1-3-5" }, keys);
        }

        [Theory]
        [MemberData(nameof(GraphCases.Named), MemberType = typeof(GraphCases))]
        public void Chains_are_connected_and_cover_all_edges(string name, TestEdge[] edges)
        {
            var chains = Run(edges, allowEdgeReuse: false);

            AssertConnected(chains);
            AssertCovered(edges, chains);
        }

        [Theory]
        [MemberData(nameof(GraphCases.Named), MemberType = typeof(GraphCases))]
        public void Chains_never_revisit_a_key(string name, TestEdge[] edges)
        {
            foreach (var chain in Run(edges, allowEdgeReuse: true))
            {
                Assert.Equal(chain.Length, chain.Select(e => e.From).Distinct().Count());
            }
        }

        [Fact]
        public void Diamond_gives_both_branches_when_edges_may_be_reused()
        {
            var keys = Keys(Run(GraphCases.Diamond(), allowEdgeReuse: true));

            Assert.Contains("1-3-5", keys);
            Assert.Contains("2-4-5", keys);
        }

        [Fact]
        public void Diamond_starters_both_reach_the_trunk()
        {
            // visiteds is per starter and covereds only change between rounds
            var chains = Run(GraphCases.Diamond(), allowEdgeReuse: false);

            Assert.Equal(2, chains.Count(c => c.Any(e => e.Id == 5)));
        }

        [Fact]
        public void Line_gives_one_chain()
        {
            var chains = Run(GraphCases.Line(), allowEdgeReuse: false);

            Assert.Equal(["1-2-3"], Keys(chains));
        }

        [Fact]
        public void Missing_next_getter_is_rejected()
        {
            // TODO: after making getterNext mandatory, assert the ArgumentNullException here
        }

        [Theory]
        [MemberData(nameof(GraphCases.Seeds), MemberType = typeof(GraphCases))]
        public void Random_graphs_hold_the_invariants(int seed)
        {
            var edges = GraphCases.Random(seed);

            foreach (var allowEdgeReuse in new[] { false, true })
            {
                var chains = Run(edges, allowEdgeReuse);

                AssertConnected(chains);
                AssertCovered(edges, chains);
            }
        }

        [Fact]
        public void Ring_is_covered_through_the_fallback_starter()
        {
            var chains = Run(GraphCases.Ring(), allowEdgeReuse: false);

            Assert.Single(chains);
            Assert.Equal(3, chains.Single().Length);
        }

        #endregion Public Methods

        #region Private Methods

        private static void AssertConnected(IEnumerable<TestEdge[]> chains)
        {
            foreach (var chain in chains)
            {
                for (var i = 1; i < chain.Length; i++)
                {
                    Assert.Equal(chain[i - 1].To, chain[i].From);
                }
            }
        }

        private static void AssertCovered(TestEdge[] edges, IEnumerable<TestEdge[]> chains)
        {
            var covered = chains.SelectMany(c => c).Select(e => e.Id).ToHashSet();

            Assert.True(
                covered.SetEquals(edges.Select(e => e.Id)),
                $"Uncovered edges: {string.Join(",", edges.Select(e => e.Id).Except(covered))}");
        }

        private static string[] Keys(IEnumerable<TestEdge[]> chains)
            => chains.Select(c => string.Join("-", c.Select(e => e.Id))).OrderBy(k => k).ToArray();

        // U-turns (A-B directly followed by B-A) are no valid continuation
        private static TestEdge[][] Run(TestEdge[] edges, bool allowEdgeReuse)
        {
            var finder = new Finder<TestEdge, int>(
                elements: edges,
                getterKey: e => e.From,
                getterNext: e => e.To,
                predicateNext: (f, t) => !(f.From == t.To && f.To == t.From),
                allowEdgeReuse: allowEdgeReuse,
                preventKeyRevisit: true,
                coverAll: true,
                reduction: ReductionType.None);

            return finder.Find()
                .Select(c => c.ToArray()).ToArray();
        }

        #endregion Private Methods
    }
}