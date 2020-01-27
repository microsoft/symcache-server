// © Microsoft Corporation. All rights reserved.

using System;
using System.Diagnostics;
using System.IO;

[DebuggerDisplay("{FullName}")]
sealed class TempDirectory : IDisposable
{
    readonly string path;
    readonly bool recursiveCleanup;

    bool disposed;

    public TempDirectory(string path) : this(path, recursiveCleanup: true)
    {

    }

    public TempDirectory(string path, bool recursiveCleanup)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        this.path = Path.GetFullPath(path);
        this.recursiveCleanup = recursiveCleanup;
    }

    public string FullName
    {
        get
        {
            if (disposed)
            {
                throw new ObjectDisposedException(null);
            }

            return path;
        }
    }

    public static TempDirectory Create()
    {
        return Create(recursiveCleanup: true);
    }

    public static TempDirectory Create(bool recursiveCleanup)
    {
        string tempDirectoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectoryPath);
        return new TempDirectory(tempDirectoryPath, recursiveCleanup);
    }

    public void Dispose()
    {
        if (!disposed)
        {
            Directory.Delete(path, recursiveCleanup);
            disposed = true;
        }
    }
}
