# Fast up-to-date

## Pseudo Algorithm

The following pseudo algorithm describes the process to perform a quick up-to-date check:

```js
if ("csproj.state" file not exists || "csproj.cache" file not exists)
  if (Restore().failed) return failed;
  return Build();

Reload:
load "csproj" // could fail
state = load "csproj.state" // could fail

// Check if we need a restore
nuget_package_reference_hash = compute_hash_from_package_reference()
if ($(RestoreSuccess) != "True" || state.nuget_package_reference_hash != nuget_package_reference_hash)
  if (Restore().success)
      goto Reload
  return failed; // waiting for filesystem change

state.nuget_package_reference_hash = nuget_package_reference_hash

// Check if all targets files hash changed
msbuild_files_hash = compute_hash_from_msbuild_files()
if (state.msbuild_files_hash != msbuild_files_hash) 
  state.msbuild_files_hash = msbuild_files_hash
  return Build(state);

// Quick update from items in the solution + transitive hash of ProjectReference
input_files_hash1 = compute_hash_items_and_project_reference()
if (state.input_files_hash1 != input_files_hash1)
  state.input_files_hash1 = input_files_hash1
  return Build(state);

// Compute the hash from the cache from resolved: Analyzer, Reference, UpToDateCheckInput, UpToDateCheckOutput, UpToDateCheckBuilt
input_files_hash2 = compute_hash_csproj_cache()
if (state.input_files_hash2 != input_files_hash2) 
  return Build(state);

// Nothing to build, everything is up to date
return;

compute_hash_from_package_reference()
  compute hash from (all PackageReference + metadata + path resolved to $(NuGetPackageFolders)) + max(timestamp)

compute_hash_from_msbuild_files()
  compute hash from (csproj + all imports + $(ProjectAssetsFile)) of filenames + max(timestamp)

compute_hash_items_and_project_reference()
  compute hash from (EvaluatedItems Compile, Content, EmbeddedResource, Analyzer, Reference, hash(ProjectReference)) + max(timestamp)

compute_hash_csproj_cache()
  compute hash from (results "csproj.cache") + max(timestamp)

Restore()
  result = restore
  if result.success
    nuget_package_reference_hash = compute_hash_from_package_reference()
    state.nuget_package_reference_hash = nuget_package_reference_hash
    store state // could fail
  return restore.status

Build(state)
  result = build
  if result.success
    state.input_files_hash2 = compute_hash_csproj_cache()
    store state // could fail
  return result
```
## Notes

### Common msbuild items

[Common MSBuild project items](https://docs.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items?view=vs-2022)


- `<Reference>` for Represents an assembly (managed) reference in the project.
- `<ProjectReference>` Represents a reference to another project. ProjectReference items are transformed into Reference items by the ResolveProjectReferences target, so any valid metadata on a Reference may be valid on ProjectReference, if the transformation process doesn't overwrite it.
- `<Compile>` Represents the source files for the compiler.
- `<EmbeddedResource>` Represents resources to be embedded in the generated assembly.
- `<Content>` Represents files that are not compiled into the project, but may be embedded or published together with it.
- `<AssemblyMetadata>` Represents assembly attributes to be generated as `[AssemblyMetadata(key, value)]`.
- `<InternalsVisibleTo>` Specifies assemblies to be emitted as `[InternalsVisibleTo(..)]` assembly attributes.

### Properties

- `ProjectAssetsFile`: json file (for restore)


### Design time builds and Up-to-date checks

* [Up-to-date Check](https://github.com/dotnet/project-system/blob/main/docs/up-to-date-check.md)
* [Up-to-date Check Implementation](https://github.com/dotnet/project-system/blob/main/docs/repo/up-to-date-check-implementation.md)
* Documentation about [Design Time Builds](https://github.com/dotnet/project-system/blob/main/docs/design-time-builds.md)
* [DesignTime targets](https://github.com/dotnet/project-system/tree/255712176d4b5dc4be054a45a5f63048aa89f4de/src/Microsoft.VisualStudio.ProjectSystem.Managed/ProjectSystem/DesignTimeTargets)

https://github.com/dotnet/project-system/blob/f4290558c82d75c42bb213da0e126d6f58254e9e/src/Microsoft.VisualStudio.ProjectSystem.Managed/ProjectSystem/UpToDate/UpToDateCheckImplicitConfiguredInputDataSource.cs#L30-L37


```c#
private static ImmutableHashSet<string> ProjectPropertiesSchemas => ImmutableStringHashSet.EmptyOrdinal
    .Add(ConfigurationGeneral.SchemaName)
    .Add(ResolvedAnalyzerReference.SchemaName)
    .Add(ResolvedCompilationReference.SchemaName)
    .Add(CopyUpToDateMarker.SchemaName)
    .Add(UpToDateCheckInput.SchemaName)
    .Add(UpToDateCheckOutput.SchemaName)
    .Add(UpToDateCheckBuilt.SchemaName);
```

- `ResolvedCompilationReference`: https://github.com/dotnet/project-system/blob/0476235d70d1b65b8062492075db6fd86995612a/src/Microsoft.VisualStudio.ProjectSystem.Managed/ProjectSystem/Rules/ResolvedCompilationReference.xaml


From [Microsoft.CSharp.DesignTime.targets](https://github.com/dotnet/project-system/blob/255712176d4b5dc4be054a45a5f63048aa89f4de/src/Microsoft.VisualStudio.ProjectSystem.Managed/ProjectSystem/DesignTimeTargets/Microsoft.CSharp.DesignTime.targets)


```xml
  <Target Name="CompileDesignTime"
          Returns="@(_CompilerCommandLineArgs)"
          DependsOnTargets="_CheckCompileDesignTimePrerequisite;Compile"
          Condition="'$(IsCrossTargetingBuild)' != 'true'">

    <ItemGroup>
      <_CompilerCommandLineArgs Include="@(CscCommandLineArgs)"/>
    </ItemGroup>
      
  </Target>
```

From [Microsoft.Managed.DesignTime.targets](https://github.com/dotnet/project-system/blob/255712176d4b5dc4be054a45a5f63048aa89f4de/src/Microsoft.VisualStudio.ProjectSystem.Managed/ProjectSystem/DesignTimeTargets/Microsoft.Managed.DesignTime.targets)


```xml

  <!-- This target collects all Analyzers in the project. -->
  <Target Name="CollectAnalyzersDesignTime" DependsOnTargets="CompileDesignTime" Returns="@(Analyzer)" />

  <!-- This target collects all the resolved references that are used to actually compile. -->
  <Target Name="CollectResolvedCompilationReferencesDesignTime" DependsOnTargets="CompileDesignTime" Returns="@(ReferencePathWithRefAssemblies)" />

  <!-- This target collects all the extra inputs for the up to date check. -->
  <Target Name="CollectUpToDateCheckInputDesignTime" DependsOnTargets="CompileDesignTime" Returns="@(UpToDateCheckInput)" />

  <!-- This target collects all the extra outputs for the up to date check. -->
  <Target Name="CollectUpToDateCheckOutputDesignTime" DependsOnTargets="CompileDesignTime" Returns="@(UpToDateCheckOutput)" />

  <!-- This target collects all the things built by the project for the up to date check. -->
  <!-- See CopyFileToOutputDirectory target -->
  <Target Name="CollectUpToDateCheckBuiltDesignTime" DependsOnTargets="CompileDesignTime" Returns="@(UpToDateCheckBuilt)">
    <ItemGroup>
      <!-- Assembly output, bin and obj -->
      <UpToDateCheckBuilt Condition="'$(CopyBuildOutputToOutputDirectory)' != 'false' and '$(SkipCopyBuildProduct)' != 'true'" Include="$(TargetPath)"/>
      <UpToDateCheckBuilt Include="@(IntermediateAssembly)"/>

      <!-- Documentation file, bin and obj -->
      <UpToDateCheckBuilt Condition="'$(_DocumentationFileProduced)'=='true'" Include="@(FinalDocFile)"/>
      <UpToDateCheckBuilt Condition="'$(_DocumentationFileProduced)'=='true'" Include="@(DocFileItem)"/>

      <!-- Symbols, bin and obj -->
      <UpToDateCheckBuilt Condition="'$(_DebugSymbolsProduced)'=='true'" Include="@(_DebugSymbolsIntermediatePath)"/>
      <UpToDateCheckBuilt Condition="'$(_DebugSymbolsProduced)'=='true' and '$(SkipCopyingSymbolsToOutputDirectory)' != 'true' and '$(CopyOutputSymbolsToOutputDirectory)' != 'false'" Include="@(_DebugSymbolsOutputPath)"/>

      <!-- app.config -->
      <!-- The property AppConfig, created in PrepareForBuild, is used instead of AppConfigWithTargetPath because GenerateSupportedRuntime
           rewrites AppConfigWithTargetPath to point to the intermediate filename. This is needed because Fast up-to-date needs to compare
           the timestamp of the source filename (AppConfig) with destination filename.
           https://github.com/dotnet/msbuild/blob/d2f9dbccd913c5612fd3a3cb78b2524fbcb023da/src/Tasks/Microsoft.Common.CurrentVersion.targets#L1152-L1165
           We skip this check if AppConfig is empty, which occurs for .NET Framework console apps. See https://github.com/dotnet/project-system/issues/6758.
      -->
      <UpToDateCheckBuilt Condition=" '@(AppConfigWithTargetPath)' != '' and '@(AppConfig)' != '' " Include="@(AppConfigWithTargetPath->'$(OutDir)%(TargetPath)')" Original="$(AppConfig)"/>
    </ItemGroup>
  </Target>

</Project>
```

### Differences between before restore and after

NuGet [restore](https://docs.microsoft.com/en-us/nuget/reference/msbuild-targets#restore-target)

After Restore:

Items
```
"SourceRoot" = "C:\\Program Files (x86)\\Microsoft Visual Studio\\Shared\\NuGetPackages\\" ["C:\\Program Files (x86)\\Microsoft Visual Studio\\Shared\\NuGetPackages\\"] #DirectMetadata=0
"SourceRoot" = "C:\\Program Files\\dotnet\\sdk\\NuGetFallbackFolder\\" ["C:\\Program Files\\dotnet\\sdk\\NuGetFallbackFolder\\"] #DirectMetadata=0
"SourceRoot" = "C:\\Users\\alexa\\.nuget\\packages\\" ["C:\\Users\\alexa\\.nuget\\packages\\"] 
```

Properties
```
"NuGetPackageFolders"="C:\\Users\\alexa\\.nuget\\packages\\;C:\\Program Files (x86)\\Microsoft Visual Studio\\Shared\\NuGetPackages;C:\\Program Files\\dotnet\\sdk\\NuGetFallbackFolder" ["C:\\Users\\alexa\\.nuget\\packages\\;C:\\Program Files (x86)\\Microsoft Visual Studio\\Shared\\NuGetPackages;C:\\Program Files\\dotnet\\sdk\\NuGetFallbackFolder"]
"NuGetPackageRoot"="C:\\Users\\alexa\\.nuget\\packages\\" ["$(UserProfile)\\.nuget\\packages\\"]
"NuGetProjectStyle"="PackageReference" ["PackageReference"]
"NuGetToolVersion"="6.0.0" ["6.0.0"]
"RestoreSuccess"="True" ["True"]
"RestoreTool"="NuGet" ["NuGet"]
```

## ProjectGraph requirements

### Properties required

```xml
      <InnerBuildProperty>TargetFramework</InnerBuildProperty>
      <InnerBuildPropertyValues>TargetFrameworks</InnerBuildPropertyValues>
```

Depending on the previous value (either `TargetFramework` or `TargetFrameworks`):

```xml
      <TargetFramework>...</TargetFramework>
      or
      <TargetFrameworks>...</TargetFrameworks>
```


### Items

ProjectReference
  - FullPath
  - GlobalPropertiesToRemove
  - SetConfiguration
  - SetPlatform
  - SetTargetFramework
  - Properties
  - AdditionalProperties
  - UndefineProperties

ProjectReferenceTargets
  - Targets
  - OuterBuild

## Misc MsBuild

- [Document the import order of the common msbuild extension points](https://github.com/dotnet/msbuild/issues/2767#issuecomment-514342730)
  
## BuildInput cache

One root cache (entry points)
- `HashListFileReference` ProjectReferencesToCacheFiles

ProjectCache
- `FileReference` FileReference
- `FileReference` BuildResultCacheFileReference
- `HashListFileProjectReference` ProjectReferencesToCacheFiles
- `List<GlobItem>` Globs
- `OrderedHashListFileReference` InputImportFiles
- `HashListFileReference` InputCompileAndContent
- `HashListFileReference` InputAssemblyReferences

ProjectConfiguration
- `List<string>` Targets
- `Dictionary<string, string>` Properties
- `List<ProjectReferenceTargetsCache>` ProjectReferenceTargets

ProjectReferenceCache
  - CacheFileReference
  - GlobalPropertiesToRemove
  - SetConfiguration
  - SetPlatform
  - SetTargetFramework
  - Properties
  - AdditionalProperties
  - UndefineProperties

FileReference
- `string` `FullPath`: full path to a file
- `DateTime` `LastWriteTimeWhenRead`: timestamp when this file reference was read

HashListFileReference
- `HashOfFilePathsAndDateTime`
- `List<FileReference>`

OrderedHashListFileReference
- `HashOfFilePathsAndDateTime`
- `List<FileReference>`

HashListFileProjectReference
- `HashOfFilePathsAndDateTime`
- `List<ProjectReferenceCache>`

ProjectReferenceTargetsCache:
- string ItemSpec
- string Targets
- bool OuterBuild

GlobItem
- string Include
- string Exclude
- string Remove



















