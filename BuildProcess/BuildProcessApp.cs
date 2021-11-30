using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Locator;

namespace BuildProcess;

public static class BuildProcessApp
{
    public static int Run(string[] args)
    {
        var latest = MSBuildLocator.QueryVisualStudioInstances().First(x => x.Version.Major == 6);
        MSBuildLocator.RegisterInstance(latest);

        var assemblyPath = Path.Combine(latest.MSBuildPath, "MSBuild.dll");

        var assembly = Assembly.LoadFile(assemblyPath);

        var type = assembly.GetType("Microsoft.Build.CommandLine.MSBuildApp");

        var mainMethod = type.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        return (int)mainMethod.Invoke(null, new object[] { args });
    }
}