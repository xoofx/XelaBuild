using System;
using System.Collections.Generic;
using System.IO;
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
    public void WriteStruct<TTransferable, TData>(TTransferable transferable) where TTransferable : ITransferable<TData> where TData : struct
    {
        transferable.Write(this);
    }

    public void WriteObject<TTransferable, TData>(TTransferable transferable) where TTransferable : ITransferable<TData> where TData : class
    {
        transferable.Write(this);
    }

    public void WriteNullableObject<TTransferable, TData>(TTransferable transferable) where TTransferable : ITransferable<TData> where TData : class
    {
        if (transferable is null)
        {
            WriteNullability(true);
        }
        else
        {
            WriteNullability(false);
            transferable.Write(this);
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

    public void WriteNullability(bool isNull)
    {
        Write(isNull ? (byte)0 : (byte)1);
    }

    public void Write(DateTime datetime)
    {
        Write(datetime.Ticks);
    }

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