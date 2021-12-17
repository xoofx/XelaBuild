using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using XelaBuild.Core.Caching;
using XelaBuild.Core.Helpers;

namespace XelaBuild.Core;

public partial class ProjectGroup
{


    private void InitializeGraphFromCachedProjectGroup()
    {
        _cachedProjectGroup = CachedProjectGroup.ReadFromFile(IndexCacheFilePath);

        var rootProject = _cachedProjectGroup.Projects.First(x => x.IsRoot);
        
        // Initialize the project graph
        var parallelism = 8;
        var entryPoint = new ProjectGraphEntryPoint(rootProject.File.FullPath, _projectCollection.GlobalProperties);
        // State the state of the group to restored by default
        // Will be evaluated by the ProjectGraph
        Restored = true;
        try
        {
            _solutionLastWriteTimeWhenRead = File.GetLastWriteTimeUtc(entryPoint.ProjectFile);
            _projectGraph = new ProjectGraph(new[] { entryPoint }, _projectCollection, CreateProjectInstanceFromCachedProjectGroup, parallelism, CancellationToken.None);
        }
        catch
        {
            Restored = false;
            throw;
        }

        // Group graph node with Project
        foreach (var graphNode in _projectGraph.ProjectNodes)
        {
            this.FindProjectState(graphNode).ProjectGraphNode = graphNode;
        }

        // Check
        bool hasErrors = false;
        foreach (var project in _projectCollection.LoadedProjects)
        {
            if (string.IsNullOrEmpty(project.GetPropertyValue("TargetFramework")))
            {
                Console.Error.WriteLine($"Error: the project {project.FullPath} is multi-targeting {project.GetPropertyValue("TargetFrameworks")}. Multi-targeting is currently not supported by this prototype");
                hasErrors = true;
            }
        }

        if (hasErrors)
        {
            throw new InvalidOperationException("Invalid error in project.");
        }
    }

    private ProjectInstance CreateProjectInstanceFromCachedProjectGroup(string projectPath, IDictionary<string, string> globalProperties, ProjectCollection projectCollection)
    {
        // Need to lock as ProjectGraph can call this callback from multiple threads
        ProjectState projectState;
        lock (_projectStates)
        {
#pragma warning disable CS8600
            if (!_projectStates.TryGetValue(projectPath, out projectState))
            {
                projectState = new ProjectState(this);
                _projectStates[projectPath] = projectState;
            }
#pragma warning restore CS8600
        }

        Debug.Assert(_cachedProjectGroup != null, nameof(_cachedProjectGroup) + " != null");
        var cachedProject = _cachedProjectGroup.Projects.First(x => x.File.FullPath == projectPath);
        var xml = ProjectRootElement.Create(new XmlTextReader(new StringReader("<Project></Project>")), projectCollection);
        xml.FullPath = cachedProject.File.FullPath;
        var instance = new ProjectInstance(xml, globalProperties, projectCollection.DefaultToolsVersion, null, projectCollection, ProjectLoadSettings.RecordEvaluatedItemElements);
        // Transfer properties
        //foreach (var cachedProperty in cachedProject.Properties)
        //{
        //    instance.SetProperty(cachedProperty.Name, cachedProperty.Value);
        //}
        instance.SetProperty("InnerBuildProperty", "true");
        instance.SetProperty("RestoreSuccess", cachedProject.IsRestoreSuccessful ? "true" : "false");
        instance.SetProperty("XelaBuildInputsCacheFile", cachedProject.BuildInputsCacheFile.FullPath);
        instance.SetProperty("XelaBuildResultCacheFile", cachedProject.BuildResultCacheFile.FullPath);
        foreach (var projectRef in cachedProject.ProjectReferences)
        {
            if (projectRef.Project is not null)
            {
                instance.AddItem("ProjectReference", projectRef.Project.File.FullPath);
            }
        }
        foreach (var projectRefTarget in cachedProject.ProjectReferenceTargets)
        {
            var item = instance.AddItem("ProjectReferenceTargets", projectRefTarget.Include);
            item.SetMetadata("Targets", projectRefTarget.Targets);
            if (projectRefTarget.OuterBuild.HasValue)
            {
                item.SetMetadata("OuterBuild", "true");
            }
        }
        instance.DefaultTargets.Add("Build");
        projectState.InitializeFromProjectInstance(instance, xml.LastWriteTimeWhenRead);

        if (!projectState.Restored)
        {
            Restored = false;
        }
        //var instance = projectState.ProjectInstance;
        //var check = instance.ExpandString("$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)");

        return instance;
    }
    
    private void WriteCachedProjectGroupFromProjectInstance()
    {
        var cachedProjectGroup = new CachedProjectGroup();

        var context = new CachedContext();

        cachedProjectGroup.SolutionFile.FullPath = _builder.Config.SolutionFilePath;
        cachedProjectGroup.SolutionFile.LastWriteTime = _solutionLastWriteTimeWhenRead;

        // Reinitialize CachedProject instances, as they need to be reference-able up-front
        Debug.Assert(_projectGraph != null, nameof(_projectGraph) + " != null");
        foreach (var project in _projectGraph.ProjectNodesTopologicallySorted)
        {
            this.FindProjectState(project).CachedProject = new CachedProject();
        }

        foreach (var project in _projectGraph.ProjectNodesTopologicallySorted)
        {
            var cachedProject = CreateCachedProject(this.FindProjectState(project), context);
            cachedProjectGroup.Projects.Add(cachedProject);
        }

        _cachedProjectGroup = cachedProjectGroup;
        _cachedProjectGroup.WriteToFile(IndexCacheFilePath);
    }

    private class CachedContext
    {
        private readonly Dictionary<ProjectImportInstance, CachedImportFileReference> _imports;
        private readonly Dictionary<string, string> _stringInstances;

        public CachedContext()
        {
            _imports = new Dictionary<ProjectImportInstance, CachedImportFileReference>();
            _stringInstances = new Dictionary<string, string>();
        }

        public CachedImportFileReference GetCachedImportFileReference(ProjectImportInstance importInstance)
        {
            if (!_imports.TryGetValue(importInstance, out var cachedImportFileReference))
            {
                cachedImportFileReference = new CachedImportFileReference(importInstance.FullPath, importInstance.LastWriteTimeWhenRead);
                _imports.Add(importInstance, cachedImportFileReference);
            }

            return cachedImportFileReference;
        }

        public string Internalize(string value)
        {
            if (!_stringInstances.TryGetValue(value, out var data))
            {
                data = value;
                _stringInstances[value] = value;
            }
            return data;
        }
    }

    private CachedProject CreateCachedProject(ProjectState state, CachedContext context)
    {
        var node = state.ProjectGraphNode;
        if (node == null) throw new InvalidOperationException("ProjectGraphNode cannot be null at this stage");
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

        //public List<CachedProperty> Properties { get; }
        //FillProperties(project, cachedProject.Properties, context);

        //public bool IsRestoreSuccessful { get; set; }
        cachedProject.IsRestoreSuccessful = project.GetPropertyValue("RestoreSuccess")?.ToLowerInvariant() == "true";

        //public CachedFileReference? ProjectAssetsCachedFile;
        var projectAssetsFilePath = project.GetPropertyValue("ProjectAssetsFile");
        if (projectAssetsFilePath != null)
        {
            projectAssetsFilePath = FileUtilities.NormalizePath(Path.Combine(project.Directory, projectAssetsFilePath));
            var fileInfo = FileUtilities.GetFileInfoNoThrow(projectAssetsFilePath);
            cachedProject.ProjectAssetsCachedFile = new CachedFileReference(projectAssetsFilePath, fileInfo?.LastWriteTimeUtc ?? DateTime.MaxValue);
        }
        else
        {
            cachedProject.ProjectAssetsCachedFile = new CachedFileReference(string.Empty, DateTime.MaxValue);
        }

        //public CachedFileReference? BuildInputsCacheFile;
        var buildInputCacheFilePath = state.GetBuildInputCacheFilePath();
        var buildInputCacheFileInfo = FileUtilities.GetFileInfoNoThrow(buildInputCacheFilePath);
        cachedProject.BuildInputsCacheFile = new CachedFileReference(buildInputCacheFilePath, buildInputCacheFileInfo?.LastWriteTimeUtc ?? DateTime.MaxValue);
        
        //public CachedFileReference? BuildResultCacheFile;
        var buildResultCacheFilePath = state.GetBuildResultCacheFilePath();
        var buildResultCacheFileInfo = FileUtilities.GetFileInfoNoThrow(buildResultCacheFilePath);
        cachedProject.BuildResultCacheFile = new CachedFileReference(buildResultCacheFilePath, buildResultCacheFileInfo?.LastWriteTimeUtc ?? DateTime.MaxValue);

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
            cachedProject.Imports.Add(context.GetCachedImportFileReference(importInstance));
        }

        //public List<CachedProjectReferenceTargets> ProjectReferenceTargets { get; }
        FillCachedProjectReferenceTargets(project, cachedProject.ProjectReferenceTargets);

        return cachedProject;
    }

    private void FillProperties(ProjectInstance project, List<CachedProperty> properties, CachedContext context)
    {
        foreach (var property in project.Properties)
        {
            if (ReservedPropertyNames.IsReservedProperty(property.Name)) continue;
            properties.Add(new CachedProperty(context.Internalize(property.Name), context.Internalize(property.EvaluatedValue)));
        }
    }

    private void FillCachedProjectReferences(ProjectInstance project, List<CachedProjectReference> cachedProjectReferences)
    {
        var projectReferences = project.GetItems("ProjectReference").ToList();
        foreach (var projectItemInstance in projectReferences)
        {
            var projectState = FindProjectState(FileUtilities.NormalizePath(Path.Combine(project.Directory, projectItemInstance.EvaluatedInclude)));
            var cachedProjectReference = new CachedProjectReference
            {
                Project = projectState.CachedProject,
                GlobalPropertiesToRemove = InternalizeString(project.GetPropertyValue(nameof(CachedProjectReference.GlobalPropertiesToRemove))),
                SetConfiguration = InternalizeString(project.GetPropertyValue(nameof(CachedProjectReference.SetConfiguration))),
                SetPlatform = InternalizeString(project.GetPropertyValue(nameof(CachedProjectReference.SetPlatform))),
                SetTargetFramework = InternalizeString(project.GetPropertyValue(nameof(CachedProjectReference.SetTargetFramework))),
                Properties = InternalizeString(project.GetPropertyValue(nameof(CachedProjectReference.Properties))),
                AdditionalProperties = InternalizeString(project.GetPropertyValue(nameof(CachedProjectReference.AdditionalProperties))),
                UndefinedProperties = InternalizeString(project.GetPropertyValue(nameof(CachedProjectReference.UndefinedProperties)))
            };
            cachedProjectReferences.Add(cachedProjectReference);
        }
    }

    private static string? InternalizeString(string? data)
    {
        return data is null ? null : string.Intern(data);
    }

    private static void FillCachedProjectReferenceTargets(ProjectInstance project, List<CachedProjectReferenceTargets> cachedProjectReferenceTargetsList)
    {
        var projectReferenceTargets = project.GetItems("ProjectReferenceTargets").ToList();
        foreach (var projectReferenceTarget in projectReferenceTargets)
        {
            var cachedProjectReferenceTarget = new CachedProjectReferenceTargets()
            {
                Include = InternalizeString(projectReferenceTarget.EvaluatedInclude) ?? string.Empty,
                Targets = InternalizeString(projectReferenceTarget.GetMetadataValue("Targets")) ?? string.Empty,
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
        // - new removes between A and B (removes that apply to A but not to B. Specially, these are placed between A's element and B's element)

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
                globs.Add(globResult);
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

        globItem.ItemType = InternalizeString(itemElement.ItemType) ?? string.Empty;

        globItem.Include = globItem.Include.Length != 0 ? $"{globItem.Include};{project.ExpandString(itemElement.Include)}" : project.ExpandString(itemElement.Include) ?? string.Empty;

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

        globItem.Exclude = globItem.Exclude;
        globItem.Remove = InternalizeString(globItem.Remove);

        return globItem;
    }

    private static void CacheInformationFromRemoveItem(ProjectInstance project, ProjectItemElement itemElement, Dictionary<string, List<string>> removeElementCache)
    {
        if (!removeElementCache.TryGetValue(itemElement.ItemType, out var cumulativeRemoveElementData))
        {
            cumulativeRemoveElementData = new List<string>();
            removeElementCache[itemElement.ItemType] = cumulativeRemoveElementData;
        }
        cumulativeRemoveElementData.Add(project.ExpandString(itemElement.Remove)!);
    }

    private static bool HasWildCards(string? text)
    {
        return text != null && text.Contains('*');
    }
}