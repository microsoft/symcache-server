// © Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Controllers
{
    // Windows authentication & authorization can be used to control access to SymCache files provided by the server.
    // [Authorize]
    public class SymCacheController : Controller
    {
        readonly SymCacheRepository repository;
        readonly IBackgroundTranscodeQueue transcodeQueue;
        readonly SymCacheTranscoder transcoder;
        readonly SemanticVersion transcoderVersion;

        public SymCacheController(SymCacheRepository repository,
            IBackgroundTranscodeQueue transcodeQueue,
            SymCacheTranscoder transcoder,
            IOptions<SymCacheOptions> options)
        {
            this.repository = repository;
            this.transcodeQueue = transcodeQueue;
            this.transcoder = transcoder;
            transcoderVersion = options.Value.TranscoderVersion;
        }

        [Route("v{major:int}.{minor:int}.{patch:int}/{pdbName}/{pdbId:guid}/{pdbAge:long=1}")]
        public async Task<IActionResult> Get(CancellationToken cancellationToken, ushort major, byte minor, byte patch,
            string pdbName, Guid pdbId, uint pdbAge)
        {
            SemanticVersion version = new SemanticVersion(major, minor, patch);

            if (version <= SymCacheVersion.MinVersion)
            {
                return NotFound();
            }

            SemanticVersion ifVersionExceedsVersion;
            string errorMessage;

            if (!TryParseIfVersionExceedsHeader(version, out ifVersionExceedsVersion, out errorMessage))
            {
                return BadRequest(errorMessage);
            }

            SymCacheKey key = new SymCacheKey(version, pdbName, pdbId, pdbAge);

            CacheResult cacheResult = repository.Find(key);

            if (cacheResult.Status == CacheStatus.PositivelyCached)
            {
                if (ifVersionExceedsVersion != null && ifVersionExceedsVersion >= cacheResult.Version)
                {
                    // The client wants only a newer version than this server can offer; don't send the server's file.
                    return NotModified();
                }

                return Success(cacheResult.Path, cacheResult.Version);
            }
            else if (cacheResult.Status == CacheStatus.NegativelyCached)
            {
                return NotFound();
            }
            else
            {
                Debug.Assert(cacheResult.Status == CacheStatus.NotCached);
            }

            if (ifVersionExceedsVersion != null && ifVersionExceedsVersion >= transcoderVersion)
            {
                // The client wants only a newer version than this server could transcoder will produce. Don't try to
                // transcode.
                return NotModified();
            }

            if (ShouldTranscodeAsynchronously(version))
            {
                // Queue the item to be transcoded on a background thread.
                transcodeQueue.Enqueue(key);
                // Ask the client to check back again in 1 second to see if we have a result available. (It is up to
                // the client to decide how many times to retry, and whether to use the suggested delay, or have
                // some static value it always uses, or use the suggested value within a min/max range).
                return NotFoundRetryAfter(TimeSpan.FromSeconds(1));
            }

            // Older clients expected the symcache file transcoded before sending a response to the HTTP GET request.
            string transcodedPath = await transcoder.TryTranscodeAsync(key, cancellationToken);

            if (transcodedPath == null)
            {
                repository.MarkNegativelyCached(key);
                return NotFound();
            }

            return Success(transcodedPath, transcoderVersion);
        }

        IActionResult Success(string path, SemanticVersion version)
        {
            return File(System.IO.File.OpenRead(path), GetContentType(version));
        }

        string GetContentType(SemanticVersion version)
        {
            return $"application/vnd.ms-symcache; version={version}";
        }

        IActionResult NotFoundRetryAfter(TimeSpan delay)
        {
            return new NotFoundRetryAfterResult(delay);
        }

        IActionResult NotModified()
        {
            return StatusCode((int)HttpStatusCode.NotModified);
        }

        bool ShouldTranscodeAsynchronously(SemanticVersion requestedVersion)
        {
            if (requestedVersion > SymCacheVersion.AlwaysAsyncTranscodeAfterVersion)
            {
                return true;
            }

            // Older clients might support asynchronous transcodes - they set this header if they do.
            return string.Equals(Request.Headers["Accept-Retry-After"], "true", StringComparison.OrdinalIgnoreCase);
        }

        bool TryParseIfVersionExceedsHeader(SemanticVersion requestedVersion,
            out SemanticVersion ifVersionExceedsVersion, out string errorMessage)
        {
            StringValues ifVersionExceedsHeader = Request.Headers["If-Version-Exceeds"];

            if (ifVersionExceedsHeader.Count == 0)
            {
                // No version range check is requested.
                ifVersionExceedsVersion = null;
                errorMessage = null;
                return true;
            }

            if (ifVersionExceedsHeader.Count > 1)
            {
                ifVersionExceedsVersion = null;
                errorMessage = "Only one If-Version-Exceeds header is permitted.";
                return false;
            }

            string rawVersion = ifVersionExceedsHeader[0];
            SemanticVersion parsedVersion;

            if (!SemanticVersion.TryParse(rawVersion, out parsedVersion))
            {
                ifVersionExceedsVersion = null;
                errorMessage = "The If-Version-Exceeds header value is not a valid version.";
                return false;
            }

            if (parsedVersion.Major == 0)
            {
                ifVersionExceedsVersion = null;
                errorMessage = "The If-Version-Exceeds header value is an unsupported version number.";
                return false;
            }

            if (parsedVersion >= requestedVersion)
            {
                ifVersionExceedsVersion = null;
                errorMessage = "The If-Version-Exceeds header value must be less than the requested version.";
                return false;
            }

            // The client version is less than the version this server's transcoder always generates; process the
            // request normally.
            ifVersionExceedsVersion = parsedVersion;
            errorMessage = null;
            return true;
        }
    }
}
