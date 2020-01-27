// © Microsoft Corporation. All rights reserved.

public class CacheResult
{
    public CacheResult(CacheStatus status, string path, SemanticVersion version)
    {
        Status = status;
        Path = path;
        Version = version;
    }

    public CacheStatus Status { get; }
    public string Path { get; }
    public SemanticVersion Version { get; }
}
