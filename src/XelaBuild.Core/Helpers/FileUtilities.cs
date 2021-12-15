using System;
using System.IO;
using System.Security;

namespace XelaBuild.Core.Helpers;

internal class FileUtilities
{
    public static void EnsureFolderForFilePath(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
    
    public static string NormalizePath(string path)
    {
        path = Path.GetFullPath(path);
        return string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == '\\' ? path : path.Replace('\\', '/'); ;
    }

    public static DateTime GetLastModifiedTimeUtc(string filePath)
    {
        var fileInfo = GetFileInfoNoThrow(filePath);
        if (fileInfo == null) return DateTime.MinValue;
        return fileInfo.LastWriteTimeUtc;
    }

    /// <summary>
    /// Gets a file info object for the specified file path. If the file path
    /// is invalid, or is a directory, or cannot be accessed, or does not exist,
    /// it returns null rather than throwing or returning a FileInfo around a non-existent file. 
    /// This allows it to be called where File.Exists() (which never throws, and returns false
    /// for directories) was called - but with the advantage that a FileInfo object is returned
    /// that can be queried (e.g., for LastWriteTime) without hitting the disk again.
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns>FileInfo around path if it is an existing /file/, else null</returns>
    public static FileInfo GetFileInfoNoThrow(string filePath)
    {
        FileInfo fileInfo;

        try
        {
            fileInfo = new FileInfo(filePath);
        }
        catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
        {
            if (NotExpectedException(e))
                throw;

            // Invalid or inaccessible path: treat as if nonexistent file, just as File.Exists does
            return null;
        }

        if (fileInfo.Exists)
        {
            // It's an existing file
            return fileInfo;
        }
        else
        {
            // Nonexistent, or existing but a directory, just as File.Exists behaves
            return null;
        }
    }

    private static bool NotExpectedException(Exception e)
    {
        if
        (
            e is UnauthorizedAccessException
            || e is ArgumentNullException
            || e is PathTooLongException
            || e is DirectoryNotFoundException
            || e is NotSupportedException
            || e is ArgumentException
            || e is SecurityException
            || e is IOException
        )
        {
            return false;
        }

        return true;
    }
}