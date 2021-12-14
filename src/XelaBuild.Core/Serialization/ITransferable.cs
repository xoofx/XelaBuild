namespace XelaBuild.Core.Serialization;

public interface ITransferable<out TData>
{
    TData Read(TransferBinaryReader reader);

    void Write(TransferBinaryWriter writer);
}