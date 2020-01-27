// © Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class SymCacheTranscoder
{
    readonly SymbolServerClient symbolServer;
    readonly SymCacheRepository repository;
    readonly SemanticVersion transcoderVersion;
    readonly string transcoderPath;
    readonly string tempDirectory;

    public SymCacheTranscoder(SymbolServerClient symbolServer, SymCacheRepository repository,
        IOptions<SymCacheOptions> options)
    {
        this.symbolServer = symbolServer;
        this.repository = repository;
        transcoderVersion = options.Value.TranscoderVersion;
        transcoderPath = options.Value.TranscoderPath;

        // Use a separate child directory for transcoding file temporarily to avoid file corruption in case of
        // concurrency. But put this temporary directory under the same parent as the final result directory, so that
        // the final, renamed file still ends up with appropriate permissions (renames on NTFS leave permissions as-is).
        tempDirectory = Path.Combine(Path.GetFullPath(options.Value.SymCacheDirectory), ".temp");
    }

    public async Task<string> TryTranscodeAsync(SymCacheKey key, CancellationToken cancellationToken)
    {
        // Check whether a result already exists. If so, don't try to transcode again.
        CacheResult result = repository.Find(key);

        if (result.Status == CacheStatus.NegativelyCached)
        {
            return null;
        }
        else if (result.Status == CacheStatus.PositivelyCached)
        {
            return result.Path;
        }

        Debug.Assert(result.Status == CacheStatus.NotCached);

        string pdbPath = await symbolServer.TryGetPdbPathAsync(key.PdbName, key.PdbId, key.PdbAge, cancellationToken);

        if (pdbPath == null)
        {
            repository.MarkNegativelyCached(key);
            return null;
        }

        // Use a separate, random directory under the temp directory so that concurrent transcodes do not collide.
        using (TempDirectory randomDirectory = new TempDirectory(Path.Combine(tempDirectory,
            Guid.NewGuid().ToString())))
        {
            string pdbDirectory = Path.Combine(randomDirectory.FullName, "pdb");

            Directory.CreateDirectory(pdbDirectory);

            // Cache the PDB from the sybol server locally, so transcoding (which is expensive) is accessing a local
            // file.
            string localPdbPath = Path.Combine(pdbDirectory, Path.GetFileName(pdbPath));

            try
            {
                File.Copy(pdbPath, localPdbPath);
            }
            catch
            {
                // The symbol server may report a PDB path that does not exist or is not accessible.
                repository.MarkNegativelyCached(key);
                return null;
            }

            string expectedOutputPath = Path.Combine(randomDirectory.FullName, SymCacheRepository.GetRelativePath(key));
            await RunTranscoderAsync(localPdbPath, randomDirectory.FullName, cancellationToken);

            if (!File.Exists(expectedOutputPath))
            {
                // Transcoding failed for some reason.
                repository.MarkNegativelyCached(key);
                return null;
            }

            string finalOutputPath = repository.GetPath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(finalOutputPath));

            try
            {
                File.Move(expectedOutputPath, finalOutputPath);
            }
            catch
            {
                // In case of concurrency, the file may already exist. If so, the transcode is done.
                if (!File.Exists(finalOutputPath))
                {
                    // But if not, the transcode failed.
                    repository.MarkNegativelyCached(key);
                    return null;
                }
            }

            return finalOutputPath;
        }
    }

    private Task RunTranscoderAsync(string localPdbPath, string outputDirectory, CancellationToken cancellationToken)
    {
        string arguments = $"-pdb \"{localPdbPath}\"";

        Dictionary<string, string> environment = new Dictionary<string, string>
        {
            { "_NT_SYMBOL_PATH", Path.Combine(Path.GetPathRoot(localPdbPath), "unused") },
            { "_NT_SYMCACHE_PATH", outputDirectory }
        };

        return ChildProcess.RunAndThrowOnFailureAsync(transcoderPath, arguments, environment, cancellationToken);
    }
}
