// © Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class SymbolServerClient
{
    // HttpClient should be long-lived, and concurrent requests are thread-safe. See:
    // https://msdn.microsoft.com/en-us/library/system.net.http.httpclient(v=vs.110).aspx#Anchor_5
    static HttpClient client = new HttpClient(new WinHttpHandler());

    Uri symbolServerUri;

    public SymbolServerClient(IOptions<SymCacheOptions> options)
    {
        symbolServerUri = options.Value.SymbolServer;
    }

    public async Task<string> TryGetPdbPathAsync(string pdbName, Guid pdbId, uint pdbAge,
        CancellationToken cancellationToken)
    {
        Uri uri = GetSymbolUri(pdbName, pdbId, pdbAge);

        using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri))
        {
            HttpResponseMessage response;

            // Limit any symbol server request to 30 seconds.
            const int millisecondsInSecond = 1000;
            const int timeoutInSeconds = 30;
            const int timeoutInMilliseconds = timeoutInSeconds * millisecondsInSecond;

            using (CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(
                timeoutInMilliseconds))
            using (CancellationTokenSource combinedCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                timeoutCancellationTokenSource.Token))
            {

                CancellationToken combinedCancellationToken = combinedCancellationTokenSource.Token;

                try
                {
                    response = await client.SendAsync(request, combinedCancellationToken);
                }
                catch (TaskCanceledException)
                {
                    return null;
                }
            }

            using (response)
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return null;
                }

                if (response.Content.Headers.ContentType.MediaType != "text/plain")
                {
                    return null;
                }

                string content = await response.Content.ReadAsStringAsync();
                const string pdbPathPrefix = "PATH:";

                if (content == null || !content.StartsWith(pdbPathPrefix))
                {
                    return null;
                }

                string pdbPath = content.Substring(pdbPathPrefix.Length);
                Debug.Assert(pdbPath != null);

                if (pdbPath.Length == 0)
                {
                    return null;
                }

                return pdbPath;
            }
        }
    }

    Uri GetSymbolUri(string pdbName, Guid pdbId, uint pdbAge)
    {
        StringBuilder pathBuilder = new StringBuilder();
        pathBuilder.Append(Uri.EscapeDataString(pdbName));
        pathBuilder.Append("/");
        pathBuilder.Append(pdbId.ToString("N").ToUpperInvariant());
        pathBuilder.Append(pdbAge.ToString("X"));
        pathBuilder.Append("/file.ptr");

        UriBuilder uriBuilder = new UriBuilder(symbolServerUri);

        if (!string.IsNullOrEmpty(uriBuilder.Path) && !uriBuilder.Path.EndsWith('/'))
        {
            uriBuilder.Path += "/" + pathBuilder.ToString();
        }
        else
        {
            uriBuilder.Path = pathBuilder.ToString();
        }

        return uriBuilder.Uri;
    }
}
