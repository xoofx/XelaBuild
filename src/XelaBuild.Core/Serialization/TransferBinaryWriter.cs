using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace XelaBuild.Core.Serialization;

public class TransferBinaryWriter : BinaryWriter
{
    protected TransferBinaryWriter()
    {
    }

    public TransferBinaryWriter(Stream output) : base(output)
    {
    }

    public TransferBinaryWriter(Stream output, Encoding encoding) : base(output, encoding)
    {
    }

    public TransferBinaryWriter(Stream output, Encoding encoding, bool leaveOpen) : base(output, encoding, leaveOpen)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteStruct<TData>(TData data) where TData : struct, ITransferable<TData>
    {
        data.Write(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteObject<TData>(TData data) where TData : class, ITransferable<TData>
    {
        data.Write(this);
    }

    public void WriteNullableObject<TData>(TData data) where TData : class, ITransferable<TData>
    {
        if (data is null)
        {
            WriteNullability(true);
        }
        else
        {
            WriteNullability(false);
            data.Write(this);
        }
    }

    public void WriteNullableStruct<TData>(TData? data) where TData : struct, ITransferable<TData>
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

    public void WriteStructsFromList<TData>(List<TData> list) where TData : struct, ITransferable<TData>
    {
        Write(list.Count);
        foreach (var item in list)
        {
            item.Write(this);
        }
    }

    public void WriteObjectsFromList<TData>(List<TData> list) where TData : class, ITransferable<TData>, new()
    {
        Write(list.Count);
        foreach (var item in list)
        {
            item.Write(this);
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
    public void WriteNullable(string value)
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
}