using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace XelaBuild.Core.Serialization;

public class BinaryTransferWriter : BinaryWriter
{
    private readonly Dictionary<object, int> _objectsToId;

    public BinaryTransferWriter(Stream output) : this(output, Encoding.Default)
    {
    }

    public BinaryTransferWriter(Stream output, Encoding encoding) : this(output, encoding, false)
    {
    }

    public BinaryTransferWriter(Stream output, Encoding encoding, bool leaveOpen) : base(output, encoding, leaveOpen)
    {
        _objectsToId = new(ReferenceEqualityComparer.Instance);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteStruct<TData>(TData data) where TData : struct, IBinaryTransferable<TData>
    {
        data.Write(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteObject<TData>(TData? data) where TData : class, IBinaryTransferable<TData>
    {
        if (data is null)
        {
            Write((byte)BinaryTransferObjectReferenceKind.Null);
            return;
        }

        if (_objectsToId.TryGetValue(data, out var objectIndex))
        {
            Write((byte)BinaryTransferObjectReferenceKind.Index);
            Write(objectIndex);
        }
        else
        {
            Write((byte)BinaryTransferObjectReferenceKind.Data);
            var id = _objectsToId.Count;
            _objectsToId.Add(data, id);

            data.Write(this);
        }
    }

    public void WriteStringShared(string? data)
    {
        if (data is null)
        {
            Write((byte)BinaryTransferObjectReferenceKind.Null);
            return;
        }

        if (_objectsToId.TryGetValue(data, out var stringIndex))
        {
            Write((byte)BinaryTransferObjectReferenceKind.Index);
            Write(stringIndex);
        }
        else
        {
            Write((byte)BinaryTransferObjectReferenceKind.Data);
            var id = _objectsToId.Count;
            _objectsToId.Add(data, id);
            Write(data);
        }
    }

    public void WriteNullableStruct<TData>(TData? data) where TData : struct, IBinaryTransferable<TData>
    {
        if (data.HasValue)
        {
            WriteNullability(false);
            data.Value.Write(this);
        }
        else
        {
            WriteNullability(true);
        }
    }

    public void WriteStructsFromList<TData>(List<TData> list) where TData : struct, IBinaryTransferable<TData>
    {
        Write(list.Count);
        foreach (var item in list)
        {
            item.Write(this);
        }
    }

    public void WriteObjectsFromList<TData>(List<TData> list) where TData : class, IBinaryTransferable<TData>, new()
    {
        Write(list.Count);
        foreach (var item in list)
        {
            WriteObject(item);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNullability(bool isNull)
    {
        Write(isNull ? (byte)0 : (byte)1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(DateTime datetime)
    {
        Write(datetime.Ticks);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNullable(bool? value)
    {
        if (value.HasValue)
        {
            WriteNullability(false);
            Write(value.Value);
        }
        else
        {
            WriteNullability(true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNullable(string? value)
    {
        if (value is null)
        {
            WriteNullability(true);
        }
        else
        {
            WriteNullability(false);
            Write(value);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCompressed(int value)
    {
        WriteCompressed(checked((uint) value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCompressed(uint value)
    {
        if (value == 0)
        {
            Write((byte)0);
            return;
        }

        do
        {
            var valueToWrite = (byte)value & 0x7F;
            value = value >> 7;
            if (value != 0)
            {
                valueToWrite |= 0x80;
            }
            Write(valueToWrite);

        } while (value != 0);
    }
}