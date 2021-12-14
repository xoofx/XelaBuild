using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TData ReadStruct<TData>(TData data) where TData : struct, ITransferable<TData>
    {
        return data.Read(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TData ReadObject<TData>(TData data) where TData : class, ITransferable<TData>
    {

        return data.Read(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TData? ReadNullableStruct<TData>(TData data) where TData : struct, ITransferable<TData>
    {
        return ReadNullability() ? null : data.Read(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TData ReadNullableObject<TData>(TData transferable) where TData : class, ITransferable<TData>
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DateTime ReadDateTime()
    {
        return new DateTime(ReadInt64(), DateTimeKind.Utc);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadNullability()
    {
        var status = ReadByte();
        return status == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool? ReadNullableBoolean()
    {
        return ReadNullability() ? null : ReadBoolean();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ReadNullableString()
    {
        return ReadNullability() ? null : ReadString();
    }
}