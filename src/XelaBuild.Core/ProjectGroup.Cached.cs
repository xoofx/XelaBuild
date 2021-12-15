using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using XelaBuild.Core.Caching;
using XelaBuild.Core.Helpers;

namespace XelaBuild.Core;

public partial class ProjectGroup
{
    private void InitializeCachedProjectGroupFromProjectInstance()
    {
        var cachedProjectGroup = new CachedProjectGroup();

        var imports = new Dictionary<ProjectImportInstance, CachedImportFileReference>();

        cachedProjectGroup.SolutionFile.FullPath = _builder.Provider.GetProjectPaths().First();
        cachedProjectGroup.SolutionFile.LastWriteTime = _solutionLastWriteTimeWhenRead;

        // Reinitialize CachedProject instances, as they need to be reference-able up-front
        foreach (var project in _projectGraph.ProjectNodesTopologicallySorted)
        {
            this.FindProjectState(project).CachedProject = new CachedProject();
        }

        foreach (var project in _projectGraph.ProjectNodesTopologicallySorted)
        {
            var cachedProject = CreateCachedProject(this.FindProjectState(project), imports);
            cachedProjectGroup.Projects.Add(cachedProject);
        }

        _cachedProjectGroup = cachedProjectGroup;
    }

    private CachedProject CreateCachedProject(ProjectState state, Dictionary<ProjectImportInstance, CachedImportFileReference> imports)
    {
        var node = state.ProjectGraphNode;
        var cachedProject = state.CachedProject;
        var project = node.ProjectInstance;

        //public bool IsRoot { get; set; }
        cachedProject.IsRoot = node.ReferencingProjects.Count == 0;

        //public string ProjectFolder { get; set; }
        cachedProject.ProjectFolder = project.Directory;

        //public CachedFileReference File;
        cachedProject.File.FullPath = project.FullPath;
        cachedProject.File.LastWriteTime = this.FindProjectState(node).ProjectInstanceLastWriteTimeWhenRead;

        //public List<CachedGlobItem> Globs { get; }
        FillGlobsFromProjectInstance(project, cachedProject.Globs);

        //public bool IsRestoreSuccessful { get; set; }
        cachedProject.IsRestoreSuccessful = project.GetPropertyValue("RestoreSuccess")?.ToLowerInvariant() == "true";

        //public CachedFileReference? ProjectAssetsCachedFile;
        var projectAssetsFilePath = project.GetPropertyValue("ProjectAssetsFile");
        if (projectAssetsFilePath != null)
        {
            projectAssetsFilePath = FileUtilities.NormalizePath(Path.Combine(project.Directory, projectAssetsFilePath));
            var fileInfo = FileUtilities.GetFileInfoNoThrow(projectAssetsFilePath);
            if (fileInfo != null)
            {
                cachedProject.ProjectAssetsCachedFile = new CachedFileReference(projectAssetsFilePath, fileInfo.LastWriteTimeUtc);
            }
        }

        //public CachedFileReference? BuildInputsCacheFile;
        var buildInputCacheFilePath = state.GetBuildResultCacheFilePath();
        var buildInputCacheFileInfo = FileUtilities.GetFileInfoNoThrow(buildInputCacheFilePath);
        cachedProject.BuildInputsCacheFile = new CachedFileReference(projectAssetsFilePath, buildInputCacheFileInfo?.LastWriteTimeUtc ?? DateTime.MaxValue);
        
        //public CachedFileReference? BuildResultCacheFile;
        var buildResultCacheFilePath = state.GetBuildResultCacheFilePath();
        var buildResultCacheFileInfo = FileUtilities.GetFileInfoNoThrow(buildResultCacheFilePath);
        cachedProject.BuildResultCacheFile = new CachedFileReference(projectAssetsFilePath, buildResultCacheFileInfo?.LastWriteTimeUtc ?? DateTime.MaxValue);

        //public List<CachedProjectReference> ProjectReferences { get; }
        FillCachedProjectReferences(project, cachedProject.ProjectReferences);

        //public List<CachedProject> ProjectDependencies { get; }
        foreach (var projectDep in node.ProjectReferences)
        {
            var projectDepState = this.FindProjectState(projectDep);
            cachedProject.ProjectDependencies.Add(projectDepState.CachedProject);
        }

        //public List<CachedFileReference> Imports { get; }
        foreach (var importInstance in project.Imports)
        {
            if (!imports.TryGetValue(importInstance, out var cachedImportFileReference))
            {
                cachedImportFileReference = new CachedImportFileReference(importInstance.FullPath, importInstance.LastWriteTimeWhenRead);
                imports.Add(importInstance, cachedImportFileReference);
            }
            cachedProject.Imports.Add(cachedImportFileReference);
        }

        //public List<CachedProjectReferenceTargets> ProjectReferenceTargets { get; }
        FillCachedProjectReferenceTargets(project, cachedProject.ProjectReferenceTargets);

        return cachedProject;
    }

    private void FillCachedProjectReferences(ProjectInstance project, List<CachedProjectReference> cachedProjectReferences)
    {
        var projectReferences = project.GetItems("ProjectReference").ToList();
        foreach (var projectItemInstance in projectReferences)
        {
            var cachedProjectReference = new CachedProjectReference
            {
                Project = FindProjectState(projectItemInstance.Project.FullPath).CachedProject,
                GlobalPropertiesToRemove = project.GetPropertyValue(nameof(CachedProjectReference.GlobalPropertiesToRemove)),
                SetConfiguration = project.GetPropertyValue(nameof(CachedProjectReference.SetConfiguration)),
                SetPlatform = project.GetPropertyValue(nameof(CachedProjectReference.SetPlatform)),
                SetTargetFramework = project.GetPropertyValue(nameof(CachedProjectReference.SetTargetFramework)),
                Properties = project.GetPropertyValue(nameof(CachedProjectReference.Properties)),
                AdditionalProperties = project.GetPropertyValue(nameof(CachedProjectReference.AdditionalProperties)),
                UndefinedProperties = project.GetPropertyValue(nameof(CachedProjectReference.UndefinedProperties))
            };
            cachedProjectReferences.Add(cachedProjectReference);
        }
    }

    private static void FillCachedProjectReferenceTargets(ProjectInstance project, List<CachedProjectReferenceTargets> cachedProjectReferenceTargetsList)
    {
        var projectReferenceTargets = project.GetItems("ProjectReferenceTargets").ToList();
        foreach (var projectReferenceTarget in projectReferenceTargets)
        {
            var cachedProjectReferenceTarget = new CachedProjectReferenceTargets()
            {
                Include = projectReferenceTarget.EvaluatedInclude,
                Targets = projectReferenceTarget.GetMetadataValue("Targets"),
                OuterBuild = string.Equals(projectReferenceTarget.GetMetadataValue("OuterBuild"), "true", StringComparison.OrdinalIgnoreCase) ? true : null,
            };
            cachedProjectReferenceTargetsList.Add(cachedProjectReferenceTarget);
        }
    }

    private static void FillGlobsFromProjectInstance(ProjectInstance project, List<CachedGlobItem> globs)
    {
        var items = project.EvaluatedItemElements.Where(x => x.ItemType is "Content" or "Compile" && (HasWildCards(x.Include) || HasWildCards(x.Exclude) || HasWildCards(x.Remove))).ToList();
        GetAllGlobs(project, items, globs);
    }

    private static void GetAllGlobs(ProjectInstance project, List<ProjectItemElement> projectItemElements, List<CachedGlobItem> globs)
    {
        if (projectItemElements.Count == 0)
        {
            return;
        }

        // Scan the project elements in reverse order and build globbing information for each include element.
        // Based on the fact that relevant removes for a particular include element (xml element A) consist of:
        // - all the removes seen by the next include statement of A's type (xml element B which appears after A in file order)
        // - new removes between A and B (removes that apply to A but not to B. Spacially, these are placed between A's element and B's element)

        // Example:
        // 1. <I Include="A"/>
        // 2. <I Remove="..."/> // this remove applies to the include at 1
        // 3. <I Include="B"/>
        // 4. <I Remove="..."/> // this remove applies to the includes at 1, 3
        // 5. <I Include="C"/>
        // 6. <I Remove="..."/> // this remove applies to the includes at 1, 3, 5
        // So A's applicable removes are composed of:
        //
        // The applicable removes for the element at position 1 (xml element A) are composed of:
        // - all the removes seen by the next include statement of I's type (xml element B, position 3, which appears after A in file order). In this example that's Removes at positions 4 and 6.
        // - new removes between A and B. In this example that's Remove 2.

        // use immutable builders because there will be a lot of structural sharing between includes which share increasing subsets of corresponding remove elements
        // item type -> aggregated information about all removes seen so far for that item type
        var removeElementCache = new Dictionary<string, List<string>>(projectItemElements.Count);

        for (var i = projectItemElements.Count - 1; i >= 0; i--)
        {
            var itemElement = projectItemElements[i];

            if (!string.IsNullOrEmpty(itemElement.Include))
            {
                var globResult = BuildGlobResultFromIncludeItem(project, itemElement, removeElementCache);

                if (globResult != null)
                {
                    globs.Add(globResult);
                }
            }
            else if (!string.IsNullOrEmpty(itemElement.Remove))
            {
                CacheInformationFromRemoveItem(project, itemElement, removeElementCache);
            }
        }
    }

    private static CachedGlobItem BuildGlobResultFromIncludeItem(ProjectInstance project, ProjectItemElement itemElement, Dictionary<string, List<string>> removes)
    {
        var globItem = new CachedGlobItem();

        globItem.Include = globItem.Include != null ? $"{globItem.Include};{project.ExpandString(itemElement.Include)}" : project.ExpandString(itemElement.Include);

        if (!string.IsNullOrEmpty(itemElement.Exclude))
        {
            globItem.Exclude = globItem.Exclude != null ? $"{globItem.Exclude};{project.ExpandString(itemElement.Exclude)}" : project.ExpandString(itemElement.Exclude);
        }

        var actualRemoves = new List<string>();
        if (removes.TryGetValue(itemElement.ItemType, out var previousRemovesForItem))
        {
            actualRemoves.AddRange(previousRemovesForItem);
        }

        if (!string.IsNullOrEmpty(itemElement.Remove))
        {
            globItem.Remove = globItem.Remove != null ? $"{globItem.Remove};{project.ExpandString(itemElement.Remove)}" : project.ExpandString(itemElement.Remove);
        }

        if (actualRemoves.Count > 0)
        {
            var actualRemovesAsStr = string.Join(';', actualRemoves);
            globItem.Remove = globItem.Remove != null ? $"{globItem.Remove};{actualRemovesAsStr}" : actualRemovesAsStr;
        }

        return globItem;
    }

    private static void CacheInformationFromRemoveItem(ProjectInstance project, ProjectItemElement itemElement, Dictionary<string, List<string>> removeElementCache)
    {
        if (!removeElementCache.TryGetValue(itemElement.ItemType, out var cumulativeRemoveElementData))
        {
            cumulativeRemoveElementData = new List<string>();
            removeElementCache[itemElement.ItemType] = cumulativeRemoveElementData;
        }
        cumulativeRemoveElementData.Add(project.ExpandString(itemElement.Include));
    }

    private static bool HasWildCards(string text)
    {
        return text != null && text.Contains('*');
    }
}