using System;

namespace XelaBuild.Core.Caching;

public record struct CachedFileReference(string FullPath, DateTime LastWriteTimeUtc);