// © Microsoft Corporation. All rights reserved.

static class SymCacheVersion
{
    // This sample only shows how to handle requests for v3.0.0+ SymCache files.
    public static readonly SemanticVersion MinVersion = new SemanticVersion(3, 0, 0);

    // Always do transcode operations asynchronously if the requested version is greater than v3.1.0. All v3.1.0 clients
    // after v3.1.0 support asynchronous transcodes.
    public static readonly SemanticVersion AlwaysAsyncTranscodeAfterVersion = new SemanticVersion(3, 1, 0);
}
