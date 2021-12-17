namespace XelaBuild.Core.Serialization;

public interface IBinaryTransferable<out TData>
{
    TData Read(BinaryTransferReader reader);

    void Write(BinaryTransferWriter writer);
}