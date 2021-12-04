using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.ProjectCache;
using Microsoft.Build.Framework;

namespace UnityProjectCachePluginExtension
{
    public class BuildResultCaching : ProjectCachePluginBase
    {
        private string _cacheFolder;

        public BuildResultCaching()
        {
            BuildResults = new Dictionary<string, BuildResult>();
        }

        public BuildResultCaching(string cacheFolder) : this()
        {
            _cacheFolder = DirectoryHelper.EnsureDirectory(cacheFolder);
        }

        private Dictionary<string, BuildResult> BuildResults { get; }

        public void ClearCaches()
        {
            lock (BuildResults)
            {
                BuildResults.Clear();
            }

            foreach (var file in Directory.EnumerateFiles(_cacheFolder))
            {
                File.Delete(file);
            }
        }

        public void AddAndSaveResult(string projectBuildKey, BuildResult result)
        {
            lock (BuildResults)
            {
                BuildResults.Add(projectBuildKey, result);
            }
            var fileToSave = GetCacheFilePath(projectBuildKey);
            using var stream = File.OpenWrite(fileToSave);
            BuildResultHelper.Serialize(result, stream);
        }

        public bool TryGetOrLoadResult(string projectBuildKey, out BuildResult buildResult)
        {
            lock (BuildResults)
            {
                if (BuildResults.TryGetValue(projectBuildKey, out buildResult))
                {
                    return true;
                }
            }
            var fileToRead = GetCacheFilePath(projectBuildKey);
            if (File.Exists(fileToRead))
            {
                using var stream = File.OpenRead(fileToRead);
                buildResult = BuildResultHelper.Deserialize(stream);
                lock (BuildResults)
                {
                    BuildResults.Add(projectBuildKey, buildResult);
                }
                return true;
            }

            return false;
        }

        private string GetCacheFilePath(string projectBuildKey)
        {
            return Path.Combine(_cacheFolder, $"{projectBuildKey}.cache");
        }


        public string GetCacheFilePath(ProjectInstance instance)
        {
            return GetCacheFilePath(Path.GetFileName(instance.FullPath));
        }

        public void DeleteResult(string projectBuildKey)
        {
            lock (BuildResults)
            {
                BuildResults.Remove(projectBuildKey);
            }
            var fileToDelete = Path.Combine(_cacheFolder, $"{projectBuildKey}.cache");
            File.Delete(fileToDelete);
        }


        public static string GetProjectBuildKeyFromBuildRequest(BuildRequestData buildRequestData)
        {
            return Path.GetFileName(buildRequestData.ProjectFullPath);
        }

        public override Task BeginBuildAsync(CacheContext context, PluginLoggerBase logger, CancellationToken cancellationToken)
        {
            if (_cacheFolder == null)
            {
                if (context.PluginSettings.TryGetValue("BuildPath", out _cacheFolder))
                {
                }
                logger.LogMessage($"BuildResultCaching loaded with the cache folder: {_cacheFolder}", MessageImportance.High);
            }

            return Task.CompletedTask;
        }

        public override Task<CacheResult> GetCacheResultAsync(BuildRequestData buildRequest, PluginLoggerBase logger, CancellationToken cancellationToken)
        {
            //buildRequest.ProjectFullPath

            var projectBuildKey = GetProjectBuildKeyFromBuildRequest(buildRequest);
            lock (BuildResults)
            {
                if (TryGetOrLoadResult(projectBuildKey, out var buildResult))
                {
                    return Task.FromResult(CacheResult.IndicateCacheHit(buildResult));
                }
                return Task.FromResult(CacheResult.IndicateNonCacheHit(CacheResultType.CacheMiss));
            }
        }

        public override Task EndBuildAsync(PluginLoggerBase logger, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}