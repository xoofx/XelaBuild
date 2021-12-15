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
        return ReadFromStream<TData>(stream);
    }

    public static TData ReadFromStream<TData>(Stream stream) where TData: class, IVersionedTransferable<TData>, new()
    {
        using var reader = new TransferBinaryReader(stream, Encoding.Default, true);
        var data = new TData();
        // Verify the magic version
        var magicVersionRead = data.MagicVersion.Read(reader);
        // Here we could handle different version if necessary
        magicVersionRead.CheckValidAgainst(data.MagicVersion);

        data.Read(reader);
        return data;
    }

    public static void WriteToFile<TData>(string filePath, TData data) where TData : class, IVersionedTransferable<TData>, new()
    {
        FileUtilities.EnsureFolderForFilePath(filePath);
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        WriteToStream(stream, data);
    }

    public static void WriteToStream<TData>(Stream stream, TData data) where TData : class, IVersionedTransferable<TData>, new()
    {
        using var writer = new TransferBinaryWriter(stream, Encoding.Default, true);
        data.MagicVersion.Write(writer);
        writer.WriteObject(data);
        // Make sure that we have flushed the entire graph
        writer.Flush();
    }
}