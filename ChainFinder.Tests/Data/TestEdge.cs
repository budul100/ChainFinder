using System.Collections.Generic;

namespace ChainFinder.Tests.Data
{
    // Minimal edge for graph tests, no dependency on the IVU model
    public record TestEdge(int Id, int From, int To);
}