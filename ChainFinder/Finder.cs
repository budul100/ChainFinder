using System;
using System.Collections.Generic;
using System.Linq;
using ChainFinder.Settings;
using ProgressWatcher.Interfaces;

namespace ChainFinder
{
    public partial class Finder<T, U>
    {
        #region Private Fields

        private readonly bool allowEdgeReuse;
        private readonly IEqualityComparer<T> comparer;
        private readonly IEqualityComparer<U> comparerKeys;
        private readonly bool coverAll;
        private readonly Dictionary<U, List<T>> edges;
        private readonly Func<T, IEnumerable<U>> getterKeys;
        private readonly Func<T, IEnumerable<U>> getterNexts;
        private readonly Func<IReadOnlyList<T>, T, bool> predicateChain;
        private readonly Func<T, T, bool> predicateNext;
        private readonly Predicate<T> predicateStarter;
        private readonly bool preferShortest;
        private readonly bool preventKeyRevisit;
        private readonly ReductionType reduction;
        private readonly T[] relevants;
        private readonly Func<T, IEnumerable<T>, IEnumerable<T>> sorterNexts;
        private readonly T[] starters;

        #endregion Private Fields

        #region Public Constructors

        public Finder(IEnumerable<T> elements, Func<T, U> getterKey, Func<T, U> getterNext,
            Predicate<T> predicateStarter = default, Func<T, T, bool> predicateNext = default,
            Func<IReadOnlyList<T>, T, bool> predicateChain = default, IEqualityComparer<T> comparer = default,
            IEqualityComparer<U> comparerKeys = default, bool allowEdgeReuse = default,
            bool preventKeyRevisit = default, bool coverAll = default,
            Func<T, IEnumerable<T>, IEnumerable<T>> sorterNexts = default,
            ReductionType reduction = ReductionType.NonOverlaps, bool preferShortest = default)
            : this(elements: elements, getterKeys: e => [getterKey(e)], getterNexts: e => [getterNext(e)],
                   predicateStarter: predicateStarter, predicateNext: predicateNext, predicateChain: predicateChain,
                   comparer: comparer, comparerKeys: comparerKeys, allowEdgeReuse: allowEdgeReuse,
                   preventKeyRevisit: preventKeyRevisit, coverAll: coverAll, sorterNexts: sorterNexts,
                   reduction: reduction, preferShortest: preferShortest)
        { }

        public Finder(IEnumerable<T> elements, Func<T, IEnumerable<U>> getterKeys,
            Func<T, IEnumerable<U>> getterNexts, Predicate<T> predicateStarter = default,
            Func<T, T, bool> predicateNext = default, Func<IReadOnlyList<T>, T, bool> predicateChain = default,
            IEqualityComparer<T> comparer = default, IEqualityComparer<U> comparerKeys = default,
            bool allowEdgeReuse = default, bool preventKeyRevisit = default, bool coverAll = default,
            Func<T, IEnumerable<T>, IEnumerable<T>> sorterNexts = default,
            ReductionType reduction = ReductionType.NonOverlaps, bool preferShortest = default)
        {
            this.getterKeys = getterKeys
                ?? throw new ArgumentNullException(nameof(getterKeys));
            this.getterNexts = getterNexts
                ?? throw new ArgumentNullException(nameof(getterNexts));

            this.predicateNext = predicateNext
                ?? ((_, __) => true);
            this.predicateChain = predicateChain
                ?? ((_, __) => true);
            this.predicateStarter = predicateStarter
                ?? (_ => true);

            this.comparer = comparer
                ?? EqualityComparer<T>.Default;
            this.comparerKeys = comparerKeys
                ?? EqualityComparer<U>.Default;

            this.allowEdgeReuse = allowEdgeReuse;
            this.preventKeyRevisit = preventKeyRevisit;
            this.coverAll = coverAll;
            this.sorterNexts = sorterNexts;
            this.reduction = reduction;
            this.preferShortest = preferShortest;

            edges = new Dictionary<U, List<T>>(this.comparerKeys);

            // Materialize once, the given sequence may be lazy or non repeatable
            relevants = elements?
                .Where(e => e != null).ToArray() ?? [];

            starters = relevants
                .Where(e => this.predicateStarter(e)).ToArray();

            InitializeEdges();
        }

        #endregion Public Constructors

        #region Public Methods

        public IEnumerable<IEnumerable<T>> Find(IPackage parentPackage = default)
        {
            using var infoPackage = parentPackage?.GetPackage(
                steps: reduction == ReductionType.None ? 1 : 2,
                status: $"Get {typeof(T).Name} chains.");

            var chains = FindAll(infoPackage);

            IEnumerable<IEnumerable<T>> result = reduction switch
            {
                ReductionType.None => chains
                    .Select(c => (IEnumerable<T>)c).ToArray(),

                ReductionType.Overlaps => GetOptimumOverlaps(
                    chains: chains,
                    parentPackage: infoPackage).ToArray(),

                ReductionType.NonOverlaps => GetOptimumNonOverlaps(
                    chains: chains,
                    parentPackage: infoPackage).ToArray(),

                _ => throw new ArgumentOutOfRangeException(nameof(reduction)),
            };

            return result;
        }

        #endregion Public Methods

        #region Private Methods

        private bool ContainsSubsequence(T[] parent, T[] child)
        {
            if (child.Length > parent.Length) return false;

            var maxStartIndex = parent.Length - child.Length;

            for (var i = 0; i <= maxStartIndex; i++)
            {
                var match = true;

                for (var j = 0; j < child.Length; j++)
                {
                    if (!comparer.Equals(
                        x: parent[i + j],
                        y: child[j]))
                    {
                        match = false;
                        break;
                    }
                }

                if (match) return true;
            }

            return false;
        }

        private List<T[]> FindAll(IPackage parentPackage = default)
        {
            var chains = new List<T[]>();
            var covereds = new HashSet<T>(comparer);

            using var infoPackage = parentPackage?.GetPackage(
                items: coverAll ? relevants : starters,
                status: $"Find {typeof(T).Name} chains.");

            var round = GetStarters(covereds);

            while (round.Length > 0)
            {
                var offset = chains.Count;

                foreach (var starter in round)
                {
                    Search(
                        current: starter,
                        path: [],
                        visiteds: new HashSet<T>(comparer),
                        pathKeys: new HashSet<U>(comparerKeys),
                        covereds: covereds,
                        chains: chains);

                    if (!coverAll)
                    {
                        infoPackage?.NextStep();
                    }
                }

                if (!coverAll)
                {
                    break;
                }

                // Edges found in this round are blocked for the next one
                var before = covereds.Count;

                foreach (var chain in chains.Skip(offset))
                {
                    covereds.UnionWith(chain);
                }

                for (var i = before; i < covereds.Count; i++)
                {
                    infoPackage?.NextStep();
                }

                round = GetStarters(covereds);
            }

            return chains;
        }

        private IEnumerable<U> GetKeys(T element)
        {
            var result = getterKeys(element)?
                .Where(k => k != null)
                .Distinct(comparerKeys).ToArray() ?? [];

            return result;
        }

        private IEnumerable<U> GetNextKeys(T element)
        {
            var result = getterNexts(element)?
                .Where(k => k != null)
                .Distinct(comparerKeys).ToArray() ?? [];

            return result;
        }

        private T[] GetNexts(T current, List<T> path, HashSet<T> visiteds, HashSet<U> pathKeys,
            HashSet<T> covereds)
        {
            var candidates = new List<T>();

            foreach (var key in GetNextKeys(current))
            {
                if (!edges.TryGetValue(
                    key: key,
                    value: out var keyEdges))
                {
                    continue;
                }

                foreach (var edge in keyEdges)
                {
                    if (!visiteds.Contains(edge)
                        && !covereds.Contains(edge)
                        && !(preventKeyRevisit && GetKeys(edge).Any(pathKeys.Contains))
                        && predicateNext(current, edge)
                        && predicateChain(path, edge))
                    {
                        candidates.Add(edge);
                    }
                }
            }

            var distincts = candidates
                .Distinct(comparer);

            var result = sorterNexts != default
                ? sorterNexts(current, distincts).ToArray()
                : distincts.ToArray();

            return result;
        }

        private IEnumerable<IEnumerable<T>> GetOptimumNonOverlaps(List<T[]> chains, IPackage parentPackage)
        {
            var orderedChains = preferShortest
                ? chains.OrderBy(c => c.Length).ToArray()
                : chains.OrderByDescending(c => c.Length).ToArray();

            using var infoPackage = parentPackage?.GetPackage(
                items: orderedChains,
                status: $"Reduce {typeof(T).Name} chains.");

            var covereds = new HashSet<T>(comparer);

            foreach (var orderedChain in orderedChains)
            {
                var segment = new List<T>();

                foreach (var edge in orderedChain)
                {
                    if (covereds.Contains(edge))
                    {
                        // Close the current segment, a chain with a hole in the
                        // middle would no longer be a connected path
                        if (segment.Count > 0)
                        {
                            covereds.UnionWith(segment);
                            yield return segment.ToArray();

                            segment = [];
                        }
                    }
                    else
                    {
                        segment.Add(edge);
                    }
                }

                if (segment.Count > 0)
                {
                    covereds.UnionWith(segment);
                    yield return segment.ToArray();
                }

                infoPackage?.NextStep();
            }
        }

        private IEnumerable<IEnumerable<T>> GetOptimumOverlaps(List<T[]> chains, IPackage parentPackage)
        {
            var orderedChains = preferShortest
                ? chains.OrderBy(c => c.Length).ToArray()
                : chains.OrderByDescending(c => c.Length).ToArray();

            using var infoPackage = parentPackage?.GetPackage(
                items: orderedChains,
                status: $"Reduce {typeof(T).Name} chains.");

            var acceptedChains = new List<T[]>();

            foreach (var candidate in orderedChains)
            {
                var isSubsequence = acceptedChains.Any(parent => ContainsSubsequence(
                    parent: parent,
                    child: candidate));

                if (!isSubsequence)
                {
                    acceptedChains.Add(candidate);
                    yield return candidate;
                }

                infoPackage?.NextStep();
            }
        }

        private T[] GetStarters(HashSet<T> covereds)
        {
            if (!coverAll)
            {
                return starters;
            }

            var uncovereds = relevants
                .Where(e => !covereds.Contains(e)).ToArray();

            if (uncovereds.Length == 0)
            {
                return [];
            }

            // Starters have no uncovered predecessor, rings fall back to the first element
            var ends = uncovereds
                .SelectMany(GetNextKeys).ToHashSet(comparerKeys);

            var result = uncovereds
                .Where(e => predicateStarter(e)
                    && !GetKeys(e).Any(ends.Contains)).ToArray();

            return result.Length > 0
                ? result
                : [uncovereds[0]];
        }

        private void InitializeEdges()
        {
            foreach (var relevant in relevants)
            {
                foreach (var key in GetKeys(relevant))
                {
                    if (!edges.TryGetValue(
                        key: key,
                        value: out var edge))
                    {
                        edge = [];
                        edges[key] = edge;
                    }

                    edge.Add(relevant);
                }
            }
        }

        // Backtracking depth first search, the recursion depth equals the chain length.
        // The path passed to predicateChain is shared and must not be stored by the caller.
        private void Search(T current, List<T> path, HashSet<T> visiteds, HashSet<U> pathKeys,
            HashSet<T> covereds, List<T[]> chains)
        {
            var keys = preventKeyRevisit
                ? GetKeys(current).ToArray()
                : [];

            path.Add(current);
            visiteds.Add(current);
            pathKeys.UnionWith(keys);

            var nexts = GetNexts(
                current: current,
                path: path,
                visiteds: visiteds,
                pathKeys: pathKeys,
                covereds: covereds);

            if (nexts.Length == 0)
            {
                chains.Add(path.ToArray());
            }

            foreach (var next in nexts)
            {
                // An earlier sibling may have consumed this edge in the meantime
                if (!visiteds.Contains(next))
                {
                    Search(
                        current: next,
                        path: path,
                        visiteds: visiteds,
                        pathKeys: pathKeys,
                        covereds: covereds,
                        chains: chains);
                }
            }

            // Backtrack
            path.RemoveAt(path.Count - 1);
            pathKeys.ExceptWith(keys);

            if (allowEdgeReuse)
            {
                visiteds.Remove(current);
            }
        }

        #endregion Private Methods
    }
}