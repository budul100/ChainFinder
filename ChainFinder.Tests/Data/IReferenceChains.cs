using System.Collections.Generic;

namespace ChainFinder.Tests.Data
{
    // Adapter around the old extension. Implement it in the test project that references the
    // IVU assemblies: map each TestEdge to a Link (SideFrom/SideTo locations, same ServiceType),
    // run the old converter logic (starter links, remainingLinks loop, GetChains<Link, Point>)
    // and map the resulting chains back to edge ids via a Link -> TestEdge dictionary.
    public interface IReferenceChains
    {
        #region Public Methods

        IReadOnlyCollection<int[]> Get(IReadOnlyCollection<TestEdge> edges, bool preventGoingTwice);

        #endregion Public Methods
    }
}