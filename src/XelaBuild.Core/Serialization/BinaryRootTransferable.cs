using System;

namespace XelaBuild.Core.Serialization;

public abstract class BinaryRootTransferable<T> : IBinaryRootTransferable<T> where T : class, IBinaryTransferable<T>
{
    public MagicVersion MagicVersion { get; set; }

    public BinaryTransferCompressionKind CompressionKind { get; set; }

    public DateTime LastWriteTime { get; set; }

    public abstract T Read(BinaryTransferReader reader);

    public abstract void Write(BinaryTransferWriter writer);
}