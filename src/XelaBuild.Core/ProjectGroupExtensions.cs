using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;

namespace XelaBuild.Core;

public static class ProjectGroupExtensions
{
    public static ProjectState FindProjectState(this ProjectGroup group, Project project)
    {
        return group.FindProjectState(project.FullPath);
    }

    public static ProjectState FindProjectState(this ProjectGroup group, ProjectInstance projectInstance)
    {
        return group.FindProjectState(projectInstance.FullPath);
    }
    public static ProjectState FindProjectState(this ProjectGroup group, ProjectGraphNode graphNode)
    {
        return group.FindProjectState(graphNode.ProjectInstance.FullPath);
    }
}