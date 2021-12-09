using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;

namespace BuildServer;

public class ProjectState
{
    public Project Project;
    
    public ProjectInstance ProjectInstance;

    public ProjectGraphNode ProjectGraphNode;

    public string GetBuildResultCacheFilePath()
    {
        return Path.Combine(Project.GetPropertyValue("IntermediateOutputPath"), $"{Path.GetFileName(Project.FullPath)}.BuildResult.cache");
    }
}