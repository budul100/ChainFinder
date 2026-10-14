using System;
using System.Collections.Generic;
using System.Linq;

namespace ChainFinder.Tests.Data
{
    public static class GraphCases
    {
        // Nodes: 1..n, edge ids are assigned in order of declaration
        public static TestEdge[] Create(params (int From, int To)[] pairs)
            => pairs.Select((p, i) => new TestEdge(i + 1, p.From, p.To)).ToArray();

        public static TestEdge[] Line()
            => Create((1, 2), (2, 3), (3, 4));

        public static TestEdge[] Ring()
            => Create((1, 2), (2, 3), (3, 1));

        // Two rings sharing node 1: 1-2-3-1 and 1-4-5-1
        public static TestEdge[] FigureEight()
            => Create((1, 2), (2, 3), (3, 1), (1, 4), (4, 5), (5, 1));

        public static TestEdge[] Diamond()
            => Create((1, 2), (1, 3), (2, 4), (3, 4), (4, 5));

        // One starter (edge 1), branches 2-4 and 3-5 merge into trunk 6
        public static TestEdge[] BranchingDiamond()
            => Create((1, 2), (2, 3), (2, 4), (3, 5), (4, 5), (5, 6));

        // Two branches merging into one trunk
        public static TestEdge[] Merge()
            => Create((1, 3), (2, 3), (3, 4), (4, 5));

        // Balloon: stem 1-2, loop 2-3-4-2
        public static TestEdge[] Balloon()
            => Create((1, 2), (2, 3), (3, 4), (4, 2));

        // Both directions of every physical segment (A-B and B-A as separate edges)
        public static TestEdge[] Bidirectional()
            => Create((1, 2), (2, 1), (2, 3), (3, 2), (3, 4), (4, 3));

        public static IEnumerable<object[]> Named()
        {
            yield return new object[] { nameof(Line), Line() };
            yield return new object[] { nameof(Ring), Ring() };
            yield return new object[] { nameof(FigureEight), FigureEight() };
            yield return new object[] { nameof(Diamond), Diamond() };
            yield return new object[] { nameof(Merge), Merge() };
            yield return new object[] { nameof(Balloon), Balloon() };
            yield return new object[] { nameof(Bidirectional), Bidirectional() };
        }

        // Seeds 0..199, graphs are reproducible from the seed shown in the test name
        public static IEnumerable<object[]> Seeds()
            => Enumerable.Range(0, 200).Select(s => new object[] { s });

        public static TestEdge[] Random(int seed)
        {
            var random = new Random(seed);
            var nodeCount = random.Next(3, 8);
            // Without self loops there are only n * (n - 1) distinct directed pairs
            var edgeCount = Math.Min(random.Next(3, 11), nodeCount * (nodeCount - 1));
            var pairs = new HashSet<(int, int)>();

            while (pairs.Count < edgeCount)
            {
                var from = random.Next(1, nodeCount + 1);
                var to = random.Next(1, nodeCount + 1);

                if (from != to)
                {
                    pairs.Add((from, to));
                }
            }

            return Create(pairs.ToArray());
        }
    }
}
