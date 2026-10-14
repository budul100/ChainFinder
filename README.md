# ChainFinder

Generic helper library to build chains (connected, ordered paths) from a set of directed elements, for example track segments, links or edges of any graph. Elements are connected by keys: the next key of one element has to match the key of the following element.

## Installation

```
dotnet add package budul.ChainFinder
```

## Usage

```csharp
using ChainFinder;
using ChainFinder.Settings;

public record Segment(string From, string To);

var segments = new[]
{
    new Segment("A", "B"),
    new Segment("B", "C"),
    new Segment("C", "D"),
};

var finder = new Finder<Segment, string>(
    elements: segments,
    getterKey: s => s.From,
    getterNext: s => s.To,
    // Do not turn around on the same physical segment
    predicateNext: (current, next) => !(current.From == next.To && current.To == next.From),
    preventKeyRevisit: true,
    coverAll: true,
    reduction: ReductionType.None);

foreach (var chain in finder.Find())
{
    // A-B, B-C, C-D
}
```

Multiple keys per element are supported through the overload with `getterKeys` and `getterNexts`.

## Options

| Parameter | Effect |
|---|---|
| `predicateStarter` | Restricts which elements may start a chain |
| `predicateNext` | Decides whether `next` may follow `current` |
| `predicateChain` | Decides whether `next` may extend the current path (read only, the list is shared) |
| `allowEdgeReuse` | `false`: an element is used once per starter. `true`: all simple paths per starter are returned |
| `preventKeyRevisit` | A chain never starts an element at a key it has already passed (no loops) |
| `coverAll` | Runs further rounds until every element is part of a chain, rings start at an arbitrary element |
| `sorterNexts` | Orders the candidates, the first one is explored first |
| `comparer`, `comparerKeys` | Custom equality, for example to treat both directions of a segment as one element |
| `reduction` | `None`: raw chains. `Overlaps`: drops chains contained in longer ones. `NonOverlaps`: cuts chains at elements already used by a longer chain |
| `preferShortest` | Orders the reduction by shortest instead of longest chain first |

## Notes

* The search is a recursive depth first search, the recursion depth equals the chain length.
* Chains of different starters may share elements unless a `reduction` is applied.
* Progress can be reported by passing an `IPackage` from [budul.ProgressWatcher](https://www.nuget.org/packages/budul.ProgressWatcher) to `Find`.

## License

MIT
