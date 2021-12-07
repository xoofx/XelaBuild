using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;

namespace BenchBuild;

/// <summary>
/// Generates a collection of C# projects with a root project, a leaf project and intermediate dependent projects.
/// The structure of the projects form a diamond:
///                 LibRoot
///             /      |      \
///   LibChild1_0 LibChild1_1 LibChild1_2  (more intermediate levels depending on the total number of projects)
///             \      |      /
///                 LibLeaf
/// </summary>
public class ProjectGenerator
{

    private readonly string _projectsRootFolder;

    private Project _rootProject;

    private readonly List<Project> _allProjects;
    private int _levelMax;

    private ProjectGenerator(string projectsRootFolder)
    {
        _projectsRootFolder = projectsRootFolder;
        _allProjects = new List<Project>();
    }

    private void Dump()
    {
        DumpProject(_rootProject, 0);
    }

    public static string Generate(string projectsRootFolder = null, ProjectGeneratorOptions options = null)
    {
        var rootFolder = projectsRootFolder ?? Path.Combine(Environment.CurrentDirectory, "projects");
        var projectGenerator = new ProjectGenerator(rootFolder);
        projectGenerator.Generate(options ?? new ProjectGeneratorOptions());
        //projectGenerator.Dump();
        //if (Directory.Exists(projectsRootFolder))
        //{
        //    try
        //    {
        //        Directory.Delete(projectsRootFolder, true);
        //    }
        //    catch
        //    {
        //        // ignore
        //    }
        //}
        projectGenerator.WriteAllProjects();

        return $"{Path.Combine(projectGenerator._projectsRootFolder, projectGenerator._rootProject.Name, projectGenerator._rootProject.Name)}.csproj"; 
    }
    
    private void Generate(ProjectGeneratorOptions options)
    {
        CreateProjectStructure(options);
    }

    private void CreateProjectStructure(ProjectGeneratorOptions options)
    {
        _rootProject = CreateProject("LibRoot");

        if (options.DownLevelRatio <= 0 || options.DownLevelRatio > 1) throw new InvalidOperationException($"Invalid value for {nameof(ProjectGeneratorOptions.DownLevelRatio)}: {options.DownLevelRatio}. Must be > 0 and <= 1");

        // Minus root project and leaf project
        var totalCount = options.TotalProjectCount - 2;

        var levels = new List<List<Project>>
        {
            new () { _rootProject }
        };

        while (totalCount > 1)
        {
            var currentLevelProjects = new List<Project>();
            _levelMax = levels.Count;

            int levelChildIndex = 0;
            foreach (var parentProject in levels[_levelMax - 1])
            {
                var currentLevelCount = (int)(totalCount * options.DownLevelRatio);
                if (currentLevelCount == 0)
                {
                    currentLevelCount = totalCount;
                }

                for (int i = 0; i < currentLevelCount; i++)
                {
                    var child = CreateProject($"LibChild{_levelMax}_{levelChildIndex}");
                    parentProject.Dependencies.Add(child);
                    currentLevelProjects.Add(child);
                    levelChildIndex++;
                }

                totalCount -= currentLevelCount;
                if (totalCount == 0) break;
            }

            levels.Add(currentLevelProjects);
        }

        // Connect lowest levels to a single leaf project (to make a diamond of dependencies)
        if (options.TotalProjectCount > 1)
        {
            _levelMax++;
            var leaf = CreateProject("LibLeaf");
            foreach (var parentProject in _allProjects)
            {
                if (parentProject == leaf) continue;
                if (parentProject.Dependencies.Count == 0)
                {
                    parentProject.Dependencies.Add(leaf);
                }
            }
        }
    }

    private void WriteAllProjects()
    {
        foreach (var project in _allProjects)
        {
            WriteProject(project);
        }
        WriteDirectoryProps();
    }

    private void WriteProject(Project project)
    {
        
        var projectFolder = Path.Combine(_projectsRootFolder, project.Name);
        var projectReferences = string.Join("\n", project.Dependencies.Select(x => 
@$"    <ProjectReference Include=""..\{x.Name}\{x.Name}.csproj"">
    </ProjectReference>"));

        // This setup for project references seems to not work
        // Asking here: https://github.com/dotnet/msbuild/issues/7100
        //        <SetTargetFramework>TargetFramework=net6.0</SetTargetFramework>
        //        <SkipGetTargetFrameworkProperties>true</SkipGetTargetFrameworkProperties>
        var csproj = $@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net6.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
{projectReferences}
  </ItemGroup>

</Project>";
        if (!Directory.Exists(projectFolder)) Directory.CreateDirectory(projectFolder);
        var csprojFilePath = Path.Combine(projectFolder, $"{project.Name}.csproj");
        File.WriteAllText(csprojFilePath, NormalizeEOL(csproj));

        var numberOfCsFiles = (int)Math.Pow(4, project.Level);
        var dependents = project.Dependencies.Select(x => x.Name).ToList();

        Console.WriteLine($"Generating {csprojFilePath} {(int)Math.Pow(4, project.Level)}");
        for (int i = 0; i < numberOfCsFiles; i++)
        {
            var className = i == 0 ? $"{project.Name}Class" : $"{project.Name}Class{i}";
            var csFilePath = Path.Combine(projectFolder, $"{className}.cs");
            var methodContent = (i > 0 || dependents .Count == 0 ? "        // empty" : string.Join("\n", dependents.Select(x => $"        {x}.{x}Class.Run();")));
            var csContent = $@"namespace {project.Name};
public static class {className} {{
    public static void Run() {{
{methodContent}
    }}
}}
";
            File.WriteAllText(csFilePath, NormalizeEOL(csContent));
        }
    }

    private void WriteDirectoryProps()
    {
        // We are double hashing MSBuildProjectFile because of this issue https://github.com/dotnet/msbuild/issues/7131
        var content = @"<Project>
    <PropertyGroup>
        <UnityBuildDir>$(MSBuildThisFileDirectory)build\</UnityBuildDir>
        <Configuration Condition=""'$(Configuration)' == ''"">Debug</Configuration>
        <OutputPath>$(UnityBuildDir)\bin\$(Configuration)\</OutputPath>
        <OutDir>$(OutputPath)</OutDir>
        <_MSBuildProjectFileHash>$(MSBuildProjectFile)-$([MSBuild]::StableStringHash($(MSBuildProjectFile)).ToString(`x8`))</_MSBuildProjectFileHash>
        <BaseIntermediateOutputPath>$(UnityBuildDir)obj\$(MSBuildProjectName)-$([MSBuild]::StableStringHash($(_MSBuildProjectFileHash)).ToString(`x8`))\</BaseIntermediateOutputPath>
        <UseCommonOutputDirectory>true</UseCommonOutputDirectory>
        <DefaultItemExcludes>$(DefaultItemExcludes);obj/**</DefaultItemExcludes>
    </PropertyGroup>
</Project>
";
    //<ItemGroup Condition=""'$(UnityBuildProcess)' == 'true'"">
    //    <ProjectCachePlugin Include=""$(MSBuildThisFileDirectory)..\UnityProjectCachePluginExtension.dll"" BuildPath =""$(UnityBuildDir)""/>
    //</ItemGroup>
        var propsFile = Path.Combine(_projectsRootFolder, "Directory.Build.props");
        File.WriteAllText(propsFile, NormalizeEOL(content));
    }

    private static string NormalizeEOL(string content)
    {
        return content.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);

    }

    private void DumpProject(Project project, int level)
    {
        var indent = new string(' ', level);
        Console.WriteLine($"{indent}{project.Name}");
        foreach (var projectDependency in project.Dependencies)
        {
            DumpProject(projectDependency, level + 1);
        }
    }

    private Project CreateProject(string name)
    {
        var project = new Project() { Name = name, Level = _levelMax };
        _allProjects.Add(project);
        return project;
    }

    private class Project
    {
        public Project()
        {
            Dependencies = new List<Project>();
        }

        public string Name { get; init; }

        public int Level { get; init; }
        
        public List<Project> Dependencies { get; }
    }
}

public class ProjectGeneratorOptions
{
    public ProjectGeneratorOptions()
    {
        TotalProjectCount = 100;
        DownLevelRatio = 0.1;
    }

    public int TotalProjectCount { get; set; }

    public double DownLevelRatio { get; set; }
}