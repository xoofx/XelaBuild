using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Execution;

namespace BuildServer;

/// <summary>
/// Internal type to manage the cache folder / cache filenames for csproj metadata results.
/// </summary>
internal class CacheFolder
{
    private readonly string _cacheFolder;
    private readonly HashAlgorithm _hasher;

    public CacheFolder(string cacheFolder)
    {
        _cacheFolder = DirectoryHelper.EnsureDirectory(cacheFolder);
        _hasher = HashAlgorithm.Create("MD5");
        if (_hasher == null) throw new InvalidOperationException("Cannot create MD5 hash service");
    }

    public string Folder => _cacheFolder;

    public void ClearCacheFolder()
    {
        foreach (var file in Directory.EnumerateFiles(_cacheFolder))
        {
            File.Delete(file);
        }
    }

    public string GetCacheFilePath(ProjectInstance instance)
    {
        var config = instance.GlobalProperties["Configuration"];
        var filename = $"{config}-{Path.GetFileName(instance.FullPath)}.{GetHash(instance)}";
        return GetCacheFilePath(filename);
    }

    private string GetCacheFilePath(string projectBuildKey)
    {
        return Path.Combine(_cacheFolder, $"{projectBuildKey}.cache");
    }

    private string GetHash(ProjectInstance instance)
    {
        var stream = new MemoryStream();
        {
            using var writer = new BinaryWriter(stream);

            // Write the full path;
            writer.Write(1);
            writer.Write(instance.FullPath);

            // Write properties (in order
            writer.Write(2);
            writer.Write(instance.GlobalProperties.Count);
            foreach (var prop in instance.GlobalProperties.OrderBy(x => x.Key))
            {
                writer.Write(prop.Key);
                writer.Write(prop.Value);
            }
        }

        byte[] computedHash = _hasher.ComputeHash(stream.ToArray());
        var builder = new StringBuilder(computedHash.Length * 2);
        foreach (byte b in computedHash)
        {
            builder.Append(b.ToString("x2"));
        }

        string result = builder.ToString();
        return result;
    }
}