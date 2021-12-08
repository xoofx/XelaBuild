using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;

namespace BuildServer;

public static class ResultsHelper
{
    public static void Verify(IReadOnlyDictionary<ProjectGraphNode, BuildResult> results)
    {
        if (results == null) return;

        bool hasErrors = false;
        foreach (var result in results)
        {
            if (result.Value.OverallResult == BuildResultCode.Failure)
            {
                Console.Error.WriteLine($"msbuild failed on project {result.Key.ProjectInstance.FullPath} {result.Value.Exception}");
                hasErrors = true;
            }
        }

        if (hasErrors)
        {
            Console.Error.WriteLine("*** Exiting due to errors above ***");
            // Exiting if we have errors
            Environment.Exit(1);
        }
    }
}