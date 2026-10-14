using System;
using System.Collections.Generic;
using System.Linq;
using ChainFinder.Tests.Data;

namespace ChainFinder.Tests.Legacy
{
    // Frozen port of the old X2IVU logic (converter GetChains + Sideable.GetChains extension),
    // reduced to TestEdge. Do not "improve" it, it is the reference.
    //
    // Mapping: Link -> TestEdge, SideFrom.Location -> From, SideTo.Location -> To.
    // Left out because they have no effect on the test graphs: viaChecker (null), iterationsMax (null),
    // allowTypeChange (all edges share one ServiceType). The sort inside the extension
    // (ServiceType, Distance > 0, IsUsed, Usage, Distance) is a tie for all test edges, and LINQ ordering
    // is stable, so the order of toGetter decides, which is the input order here.
    public class ReferenceChains
        : IReferenceChains
    {
        #region Public Methods

        public IReadOnlyCollection<int[]> Get(IReadOnlyCollection<TestEdge> edges, bool preventGoingTwice)
        {
            var result = new List<int[]>();

            // Same collection type as in the converter, enumeration order is part of the behavior
            var remainingLinks = edges.ToHashSet();

            while (remainingLinks.Count > 0)
            {
                var endLocations = remainingLinks
                    .Select(l => l.To).ToHashSet();
                var startLinks = remainingLinks
                    .Where(l => !endLocations.Contains(l.From)).ToArray();

                if (startLinks.Length == 0)
                {
                    startLinks = remainingLinks
                        .Take(1).ToArray();
                }

                var usedLinks = new HashSet<TestEdge>();

                foreach (var startLink in startLinks)
                {
                    var chains = GetChains(
                        current: startLink,
                        toGetter: f => edges.Where(t => t.From == f.To
                            && remainingLinks.Contains(t)
                            && !IsChangeOfDirection(f, t)),
                        preventGoingTwice: preventGoingTwice,
                        givens: default,
                        toucheds: []).ToArray();

                    foreach (var chain in chains)
                    {
                        var chainLinks = chain.ToArray();
                        usedLinks.UnionWith(chainLinks);

                        result.Add(chainLinks.Select(l => l.Id).ToArray());
                    }
                }

                if (usedLinks.Count == 0)
                {
                    break;
                }

                remainingLinks.ExceptWith(usedLinks);
            }

            return result;
        }

        #endregion Public Methods

        #region Private Methods

        private static IEnumerable<IEnumerable<TestEdge>> GetChains(TestEdge current,
            Func<TestEdge, IEnumerable<TestEdge>> toGetter, bool preventGoingTwice,
            IEnumerable<TestEdge> givens, HashSet<TestEdge> toucheds)
        {
            var currents = givens?.ToList()
                ?? [];

            currents.Add(current);

            // Note: givens does not contain current, the check lags one edge behind.
            // TODO: verify against the real SelectLocations, here only SideFrom of the givens counts.
            var givenLocations = givens?
                .Select(g => g.From).ToHashSet() ?? [];

            var nexts = toGetter(current)
                .Where(b => !givenLocations.Contains(b.From)
                    && !IsChangeOfDirection(current, b)
                    && (!preventGoingTwice || !toucheds.Contains(b))).ToArray();

            if (nexts.Length > 0)
            {
                foreach (var next in nexts)
                {
                    toucheds.Add(next);

                    // Evaluated eagerly per next, like in the original
                    var results = GetChains(
                        current: next,
                        toGetter: toGetter,
                        preventGoingTwice: preventGoingTwice,
                        givens: currents,
                        toucheds: toucheds).ToArray();

                    foreach (var result in results)
                    {
                        yield return result;
                    }
                }
            }
            else
            {
                yield return currents;
            }
        }

        // U-turn on the same physical segment
        private static bool IsChangeOfDirection(TestEdge from, TestEdge to)
            => from.From == to.To
                && from.To == to.From;

        #endregion Private Methods
    }
}