// © Microsoft Corporation. All rights reserved.

using System;

public class SymCacheKey : IEquatable<SymCacheKey>
{
    public SymCacheKey(SemanticVersion version, string pdbName, Guid pdbId, uint pdbAge)
    {
        Version = version;
        PdbName = pdbName;
        PdbId = pdbId;
        PdbAge = pdbAge;
    }

    public SemanticVersion Version { get; }
    public string PdbName { get; }
    public Guid PdbId { get; }
    public uint PdbAge { get; }

    public override int GetHashCode()
    {
        // PdbId is likely unique and a better hash code than trying to combine with other properties.
        return PdbId.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
        {
            return false;
        }

        if (obj is SymCacheKey other)
        {
            return Equals(other);
        }
        else
        {
            return false;
        }
    }

    public bool Equals(SymCacheKey other)
    {
        if (other == null)
        {
            return false;
        }

        return Version == other.Version && PdbName == other.PdbName && PdbId == other.PdbId &&
            PdbAge == other.PdbAge;
    }
}
