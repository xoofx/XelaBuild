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
    private readonly List<object> _objects;

    public TransferBinaryReader(Stream input) : this(input, Encoding.UTF8)
    {
    }

    public TransferBinaryReader(Stream input, Encoding encoding) : this(input, encoding, false)
    {
    }

    public TransferBinaryReader(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
    {
        _objects = new();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TData ReadStruct<TData>(TData data) where TData : struct, ITransferable<TData>
    {
        return data.Read(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TData ReadObject<TData>(TData data) where TData : class, ITransferable<TData>
    {
        var kind = (TransferObjectReferenceKind)ReadByte();
        switch (kind)
        {
            case TransferObjectReferenceKind.Index:
            {
                var id = ReadInt32();
                return (TData)_objects[id];
            }
            case TransferObjectReferenceKind.Data:
            {
                _objects.Add(data);
                return data.Read(this);
            }
            case TransferObjectReferenceKind.Null:
                return null;
            default:
                throw new InvalidDataException($"Invalid reference kind {kind} to a {data}. Expecting only 1 or 2.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ReadStringShared()
    {
        var kind = (TransferObjectReferenceKind)ReadByte();
        switch (kind)
        {
            case TransferObjectReferenceKind.Index:
            {
                var id = ReadInt32();
                return (string)_objects[id];
            }
            case TransferObjectReferenceKind.Data:
            {
                var data = ReadString();
                _objects.Add(data);
                return data;
            }
            case TransferObjectReferenceKind.Null:
                return null;
            default:
                throw new InvalidDataException($"Invalid reference kind {kind} to a string. Expecting only 1 or 2.");
        }
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
            item = ReadObject(item);
            list.Add(item);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DateTime ReadDateTime()
    {
        var value = ReadInt64();
        return new DateTime(value, DateTimeKind.Utc);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadCompressedInt32()
    {
        return checked((int)ReadCompressedUInt32());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadCompressedUInt32()
    {
        uint value = 0;
        while(true)
        {
            var valueRead = ReadByte();
            if ((sbyte) valueRead < 0)
            {
                valueRead = (byte)(valueRead & 0x7F);
            }
            else
            {
                break;
            }
            value = value << 7;
            value |= valueRead;
        }

        return value;
    }
}