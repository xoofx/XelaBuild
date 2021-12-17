using System;

namespace XelaBuild.Core.Serialization;

public interface IBinaryRootTransferable<out T> : IBinaryTransferable<T> 
{
    MagicVersion MagicVersion { get; set; }

    BinaryTransferCompressionKind CompressionKind { get; set; }

    DateTime LastWriteTime { get; set; }
}