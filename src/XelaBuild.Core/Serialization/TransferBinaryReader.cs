using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace XelaBuild.Core.Serialization;

public class TransferBinaryReader : BinaryReader
{
    public TransferBinaryReader(Stream input) : base(input)
    {
    }

    public TransferBinaryReader(Stream input, Encoding encoding) : base(input, encoding)
    {
    }

    public TransferBinaryReader(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
    {
    }
    public TData ReadStruct<TTransferable, TData>(TTransferable transferable) where TTransferable : ITransferable<TData> where TData : struct
    {
        return transferable.Read(this);
    }

    public TData ReadObject<TTransferable, TData>(TTransferable transferable) where TTransferable : ITransferable<TData> where TData : class
    {

        return transferable.Read(this);
    }

    public TData ReadNullableObject<TTransferable, TData>(TTransferable transferable) where TTransferable : ITransferable<TData> where TData : class
    {
        return ReadNullability() ? null: transferable.Read(this);
    }

    public void ReadStructsToList<TData>(List<TData> list) where TData : struct, ITransferable<TData>
    {
        var length = ReadInt32();
        list.Capacity = length;
        for (int i = 0; i < length; i++)
        {
            var item = new TData();
            item.Read(this);
            list.Add(item);
        }
    }

    public void ReadObjectsToList<TData>(List<TData> list) where TData : class, ITransferable<TData>, new()
    {
        var length = ReadInt32();
        list.Capacity = length;
        for (int i = 0; i < length; i++)
        {
            var item = new TData();
            item.Read(this);
            list.Add(item);
        }
    }

    public DateTime ReadDateTime()
    {
        return new DateTime(ReadInt64(), DateTimeKind.Utc);
    }

    public bool ReadNullability()
    {
        var status = ReadByte();
        return status == 0;
    }
    
    public bool? ReadNullableBoolean()
    {
        return ReadNullability() ? null : ReadBoolean();
    }

    public string ReadNullableString()
    {
        return ReadNullability() ? null : ReadString();
    }
}