using System.IO;
using System.Text;
using XelaBuild.Core.Helpers;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core.Caching;

public static class CachedBinaryHelper
{
    public static TData ReadFromFile<TData>(string filePath) where TData : class, IVersionedTransferable<TData>, new()
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var data = ReadFromStream<TData>(stream);
        data.LastWriteTimeWhenRead = File.GetLastWriteTimeUtc(filePath);
        return data;
    }

    public static TData ReadFromStream<TData>(Stream stream) where TData : class, IVersionedTransferable<TData>, new()
    {
        var data = new TData();
        using var reader = new TransferBinaryReader(stream, Encoding.UTF8, true);
        // Verify the magic version
        var magicVersionRead = data.MagicVersion.Read(reader);
        // Here we could handle different version if necessary
        magicVersionRead.CheckValidAgainst(data.MagicVersion);

        data = reader.ReadObject(data);
        return data;
    }

    public static void WriteToFile<TData>(string filePath, TData data)
        where TData : class, IVersionedTransferable<TData>, new()
    {
        FileUtilities.EnsureFolderForFilePath(filePath);
        using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            WriteToStream(stream, data);
        }
        data.LastWriteTimeWhenRead = File.GetLastWriteTimeUtc(filePath);
    }

    public static void WriteToStream<TData>(Stream stream, TData data) where TData : class, IVersionedTransferable<TData>, new()
    {
        using var writer = new TransferBinaryWriter(stream, Encoding.UTF8, true);
        data.MagicVersion.Write(writer);
        writer.WriteObject(data);
        // Make sure that we have flushed the entire graph
        writer.Flush();
    }
}