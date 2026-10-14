namespace ChainFinder.Settings
{
    public enum ReductionType
    {
        // Raw maximal chains, nothing is dropped or cut
        None,

        // Drops chains that are a contiguous part of a longer chain
        Overlaps,

        // Cuts chains at edges already used by a longer chain
        NonOverlaps
    }
}
