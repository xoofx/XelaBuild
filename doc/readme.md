# Notes

## Common msbuild items

[Common MSBuild project items](https://docs.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items?view=vs-2022)


- `<Reference>` for Represents an assembly (managed) reference in the project.
- `<ProjectReference>` Represents a reference to another project. ProjectReference items are transformed into Reference items by the ResolveProjectReferences target, so any valid metadata on a Reference may be valid on ProjectReference, if the transformation process doesn't overwrite it.
- `<Compile>` Represents the source files for the compiler.
- `<EmbeddedResource>` Represents resources to be embedded in the generated assembly.
- `<Content>` Represents files that are not compiled into the project, but may be embedded or published together with it.
- `<AssemblyMetadata>` Represents assembly attributes to be generated as `[AssemblyMetadata(key, value)]`.
- `<InternalsVisibleTo>` Specifies assemblies to be emitted as `[InternalsVisibleTo(..)]` assembly attributes.

## Properties

- `ProjectAssetsFile`: json file (for restore)


## Design time builds and Up-to-date checks

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
