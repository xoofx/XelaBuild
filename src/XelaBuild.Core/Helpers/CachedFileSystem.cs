using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace XelaBuild.Core.Helpers;

public class CachedFileSystem
{
    private readonly ConcurrentDictionary<string, DateTime> _lastWriteTimeFiles;

    public CachedFileSystem()
    {
        _lastWriteTimeFiles = new ConcurrentDictionary<string, DateTime>(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }

    public DateTime GetLastWriteTimeUtc(string filePath)
    {
        return _lastWriteTimeFiles.GetOrAdd(filePath, x => FileUtilities.GetLastWriteTimeUtc(x));
    }
}