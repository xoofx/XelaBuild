using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using XelaBuild.Core.Helpers;

namespace XelaBuild.Core.Serialization;

public static class BinaryTransfer
{
    public static TData ReadFromFile<TData>(string filePath) where TData : class, IBinaryRootTransferable<TData>, new()
    {
        filePath = FileUtilities.NormalizePath(filePath);
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var data = ReadFromStream<TData>(stream);
        data.LastWriteTime = File.GetLastWriteTimeUtc(filePath);
        return data;
    }

    public static TData ReadFromStream<TData>(Stream stream) where TData : class, IBinaryRootTransferable<TData>, new()
    {
        var data = new TData();
        Span<byte> header = stackalloc byte[8];
        int bytesRead = stream.Read(header);
        if (bytesRead != 8) throw new InvalidDataException("Invalid data stream. Expecting a header of a length of 8 bytes");
        var (versionRead, compressionKind) = data.MagicVersion.ReadFrom(header);
        // Verify the magic version
        // Here we could handle different version if necessary
        // e.g not return TData, pass it to the BinaryTransferReader...
        versionRead.CheckValidAgainst(data.MagicVersion);

        // Get Decompress stream
        using var inputStream = GetDecompressStream(stream, compressionKind);
        using var reader = new BinaryTransferReader(inputStream, Encoding.UTF8, true);

        data = reader.ReadObject(data);
#pragma warning disable CS8603 // Not null
        return data;
#pragma warning restore CS8603
    }

    public static void WriteToFile<TData>(string filePath, TData data)
        where TData : class, IBinaryRootTransferable<TData>, new()
    {
        filePath = FileUtilities.NormalizePath(filePath);
        FileUtilities.EnsureFolderForFilePath(filePath);
        using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            WriteToStream(stream, data);
        }
        data.LastWriteTime = File.GetLastWriteTimeUtc(filePath);
    }

    public static void WriteToStream<TData>(Stream stream, TData data) where TData : class, IBinaryRootTransferable<TData>, new()
    {
        // Write the header
        Span<byte> header = stackalloc byte[8];
        data.MagicVersion.WriteTo(header, data.CompressionKind);
        stream.Write(header);
        // Write the content
        using var inputStream = GetCompressStream(stream, data.CompressionKind);
        using var writer = new BinaryTransferWriter(inputStream, Encoding.UTF8, true);
        writer.WriteObject(data);
        // Make sure that we have flushed the entire data
        writer.Flush();
        // Flush the input stream if we have an intermediate stream
        if (stream != inputStream)
        {
            inputStream.Flush();
        }
    }

    private static Stream GetDecompressStream(Stream stream, BinaryTransferCompressionKind compressionKind)
    {
        switch (compressionKind)
        {
            case BinaryTransferCompressionKind.None:
                return stream;
            case BinaryTransferCompressionKind.Brotli:
                return new BrotliStream(stream, CompressionMode.Decompress, true);
            default:
                throw new ArgumentOutOfRangeException(nameof(compressionKind), compressionKind, null);
        }
    }
    private static Stream GetCompressStream(Stream stream, BinaryTransferCompressionKind compressionKind)
    {
        switch (compressionKind)
        {
            case BinaryTransferCompressionKind.None:
                return stream;
            case BinaryTransferCompressionKind.Brotli:
                return new BufferedStream(new BrotliStream(stream, CompressionLevel.Fastest, true), 16384);
            default:
                throw new ArgumentOutOfRangeException(nameof(compressionKind), compressionKind, null);
        }
    }
}