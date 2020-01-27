// © Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Options;
using System;
using System.IO;

public class SymCacheRepository
{
    readonly string directory;

    public SymCacheRepository(IOptions<SymCacheOptions> options)
    {
        directory = Path.GetFullPath(options.Value.SymCacheDirectory);
    }

    public CacheResult Find(SymCacheKey key)
    {
        string path = GetPath(key);

        if (File.Exists(path))
        {
            return new CacheResult(CacheStatus.PositivelyCached, path, key.Version);
        }

        string negativeCachePath = GetNegativeCachePath(key);

        if (File.Exists(negativeCachePath))
        {
            try
            {
                DateTimeOffset negativeCacheExpires = DateTimeOffset.Parse(File.ReadAllText(negativeCachePath));

                if (negativeCacheExpires.UtcDateTime < DateTimeOffset.UtcNow)
                {
                    File.Delete(negativeCachePath);
                }

                return new CacheResult(CacheStatus.NegativelyCached, null, null);
            }
            catch
            {
                // Concurrent requests could mean the file read or delete above fails. Treat the negative cache entry as
                // non-existent in that case.
            }
        }

        return new CacheResult(CacheStatus.NotCached, null, null);
    }

    public void MarkNegativelyCached(SymCacheKey key)
    {
        // Treat negative cache entry as valid for 1 day.
        string contents = DateTimeOffset.UtcNow.AddDays(1).ToString("o");
        string relativePath = GetNegativeCacheRelativePath(key);
        string path = Path.Combine(directory, relativePath);

        // Ensure parent directories exist.
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        File.WriteAllText(path, contents);
    }

    public string GetPath(SymCacheKey key)
    {
        return Path.Combine(directory, GetRelativePath(key));
    }

    string GetNegativeCachePath(SymCacheKey key)
    {
        return Path.Combine(directory, GetNegativeCacheRelativePath(key));
    }

    internal static string GetRelativePath(SymCacheKey key)
    {
        return $@"{key.PdbName}\{key.PdbId:N}{key.PdbAge:X}\{key.PdbName}-v{key.Version}.symcache";
    }

    static string GetNegativeCacheRelativePath(SymCacheKey key)
    {
        return Path.ChangeExtension(GetRelativePath(key), "negativesymcache");
    }
}
