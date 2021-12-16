using System;
using System.IO;
using XelaBuild.Core.Serialization;

namespace XelaBuild.Core.Caching;

public interface IVersionedTransferable<out T> : ITransferable<T> 
{
    CachedMagicVersion MagicVersion { get; set; }

    DateTime LastWriteTimeWhenRead { get; set; }
}