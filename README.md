---
page_type: sample
languages:
- csharp
products:
- dotnet
description: "Sample symcache HTTP server"
urlFragment: "symcache-server"
---

# Official Microsoft Sample

[Windows Performance Analyzer](https://docs.microsoft.com/en-us/windows-hardware/test/wpt/windows-performance-analyzer)
and [TraceProcessor](https://aka.ms/traceprocessing) use the
[SymCache](https://docs.microsoft.com/en-us/windows-hardware/test/wpt/loading-symbols#symcache-path) format, which is a
cache of some of the data stored in a PDB. When loading symbols, WPA and TraceProcessor need to create SymCache files
from PDB files, which can be expensive.

Rather than having each client do the work of downloading a PDB and transcoding it into a SymCache file, the SymCache
HTTP protocol supports having a central server do the work once and then provide SymCache files directly to clients,
paying the cost of dowloading the PDB and transcoding only once, saving only the smaller SymCache files, and providing
these cached files directly to any subsequent callers.

Transcoding PDBs into SymCache files requires a tool such as symcachegen.exe, which is planned to be made available as
part of a future release of the Windows Performance Toolkit release in a future Windows SDK. Until symcachegen.exe is
publicly available, this sample serves as documentation but cannot be run.

## Contents

| File/folder       | Description                                |
|-------------------|--------------------------------------------|
| `src`             | Sample source code.                        |
| `.gitignore`      | Define what to ignore at commit time.      |
| `CHANGELOG.md`    | List of changes to the sample.             |
| `CONTRIBUTING.md` | Guidelines for contributing to the sample. |
| `README.md`       | This README file.                          |
| `LICENSE`         | The license for the sample.                |

## Prerequisites

The SymCache HTTP protocol supports (but does not require) Negotiate authentication, which may work most easily on
Windows. The sample has been tested only on Windows.

## Setup

Open sample.sln in Visual Studio and build.

## Running the sample

This sample will not be able to be used until symcachegen.exe is available as part of a future Windows Performance
Toolkit release.

Once symcachegen.exe is available, use the following steps to run the sample:

1. Create a directory to hold the server's cache of SymCache files. Set the SymCacheDirectory environment variable to
   this directory.
2. Set the SymbolServer environment variable to the URL that serves as the upstream symbols server for this symcache
   server.
3. Set the TranscoderPath environment variable to the full path to symcachegen.exe, including the file name.
4. Run symcachegen.exe once to determine what version of the format it produces. Set the TranscoderVersion environment
   variable to this verison. (As of this writing, the current SymCache format version is 3.1.0.)
    1. To run symcachegen.exe, set the environment variables _NT_SYMBOL_PATH and _NT_SYMCACHE_PATH to any directory.
    2. Run symcachegen.exe -pdb \<path to some pdb\>.
    3. Check the output file produced in _NT_SYMCACHE_PATH. The transcoder version is in the end of the symcache file
       name. For example, if the file produced is my.pdb-v3.2.1.symcache, the transcoder version is v3.2.1.

## Key concepts

### SymCache HTTP Protocol

A SymCache HTTP client, such as WPA or TraceProcessor, can get SymCache files in three ways:
1. It can load them from a local directory of previously-created SymCache files.
2. It can find PDBs from symbol servers and locally download and transcode these files to the SymCache format.
3. It can request SymCache files directly from an HTTP server and download them to a local SymCache directory.

This third option uses the SymCache HTTP protocol documented in this section. To use a SymCache HTTP server, configure a
local SymCache directory, and then use the server's URL in the list of alternate SymCache search locations. For example,
if you store your files in C:\SymCache and have a local symcache server at https[]()://symcache, your _NT_SYMCACHE_PATH
would be:
C:\SymCache*https[]()://symcache

Note that a local cache directory is required when using a SymCache HTTP server - there must be a location where the
client can download and save the SymCache files the server provides.

Current releases of WPA and TraceProcessor support version 3.0.0+ of the SymCache format (3.1.0 as of this writing).
(Older SymCache formats had different HTTP protocol details that are not described here since the latest releases do not
use them anymore.) When a client asks a server for a SymCache file, it makes an HTTP GET request of the form:
\<serverUri\>/v\<major\>.\<minor\>.\<patch\>/\<pdbName\>/\<pdbId\>[/\<pdbAge\>]
The major, minor and patch portions indicate the version of the SymCache format of the file requested.
The pdbName is the file name (not including any path) of the PDB.
The pdbId is a GUID indicating the unique identifier of the specific PDB requested (also called the RSDS key).
The pdbAge segment can be ommitted if it is 1.

If the server has an exact match for that SymCache file already available, it replies with a 200 OK response, providing
the content directly in the response body, or 302 response, redirecting to another URL that will provide the content.
All successfull responses must include a Content-Type header with application/vnd.ms-symcache as the media type.

If the server knows it does not have a match for the requested SymCache file, it replies with a 404 Not Found response.

If the server does not yet have a match for the requested SymCache file and would need to check, the behavior depends on
the client. If the client requests format version 3.1.0 or earlier and does not pass an Allow-Retry-After: true header,
the server waits to send a response until it has determined a final answer, holding the HTTP request in the meantime (it
tries to search for a PDB and transcode synchronously within the HTTP request). If the client requests a format version
greater than 3.1.0, or if the client provides an Allow-Retry-After: true header in the request, the server responds
immediately with a 404 Not Found response but with a Retry-After: \<seconds\> header in the response. The client will
retry the same request until either: a) the server starts reponding with a successful response, b) the server starts
responding with a final 404 Not Found response (without a Retry-After header), or c) the client decides it is not
willing to wait anymore, in which case it tries any other configured ways of getting a SymCache file.

A SymCache server is not required to provide a SymCache client with a file in the exact same format version as the
client requested; it may return any format version is long as it is compatible with the client. For example (with
hypothetical future version numbers), if the client requests version 4.1.0, the server may return 3.2.0 or 4.5.0,
because a 4.x client is expected to understand any format before 4.x as well as be compatible with any non-breaking
changes within 4.x after its version. However, a server could not respond to client request for version 4.1.0 with
version 5.0.0, because a new major version number means the format contains breaking changes an old client could not
understand.

Unless the server returns the exact version requested, it must inform the client of the version it provided via a
version parameter in the Content-Type response header. For example, the server might send "Content-Type:
application/vnd.ms-symcache; version=3.1.0" as a response header.

SymCache clients also may have an older SymCache format version of the file already present. In that case, they include
an If-Version-Exceeds: \<major\>.\<minor\>.\<patch\> header in the request. If the server cannot provide a newer version
than the client already has, it responds with 304 Not Modified to avoid doing unnecessary work in sending the client a
file it already has. For example, consider an update to the SymCache format to version 3.2.0, including some new data
that is not currently included in version 3.1.0. If a new client starts using version 3.2.0 of the SymCache format, it
will want to get the new data from version 3.2.0 files where possible, but it may already have version 3.1.0 content for
a number of files cached locally. If the server's transcoder is still running version 3.1.0, there is no need for the
server to send the same file back to the client that the client already has, and attempting to transcode again from the
PDB would not help since the server's transcoder is still on the older verison. Alternatively, consider the case where
the client is running version 3.2.0 but does not have any SymCache file for some PDB and the PDB has since been deleted.
If the server has a version 3.1.0 file cached, the client will prefer that file to no file, and it will make a request
to the server without any If-Version-Exceeds request header.

A SymCache client will authenticate on demand using the Negotiate protocol if the server requires it to do so via a 401
response, allowing the server to support both authentication and authorization of SymCache requests.

The SymCache format uses [semantic versioning](https://semver.org/spec/v1.0.0.html) starting with version 3.0.0.

### Sample SymCacheHttpServer

As it gets requests for SymCache files, the sample SymCacheHttpServer in this repository uses an upsteram symbol server
to find the paths to PDBs it needs to download and then transcode into SymCache format. This sample supports only a
subset of the symbol server protocol - specifically, it supports only symbol servers that always return results for
file.ptr requests. Other symbol servers may use different URL formats, such as \<pdbname\>.pdb, \<pdbname\>.pd_, which
the sample does not include code to support.

A SymCache HTTP server can run multiple transcoders, one for each major version of the SymCache format. (Since the
SymCache format uses semantic versioning, running more than one transcoder per major version is not useful - any client
will support new minor versions within the same major version, so the server can always just run the latest transcoder
within each major version.) This sample only includes code to run a single transcoder version at one time, and it
ignores files in its cache directory from any other version. (If the sample were upgraded to a newer transcoder version,
it would stop providing files it had previously cached.)

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
