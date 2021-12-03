using System;
using System.IO;

namespace UnityProjectCachePluginExtension;

public static class DirectoryHelper
{
    public static string EnsureDirectory(string directory)
    {
        if (directory == null) throw new ArgumentNullException(nameof(directory));
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return directory;
    }
}