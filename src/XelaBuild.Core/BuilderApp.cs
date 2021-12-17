using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Mono.Options;
using XelaBuild.Core.Helpers;

namespace XelaBuild.Core;

public class BuilderApp
{
    public BuilderApp()
    {
    }

    public int Run(string[] args)
    {
        var exeName = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly()?.Location)?.ToLowerInvariant();
        bool showHelp = false;

        var assemblyInfoVersion = typeof(BuilderApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var version = assemblyInfoVersion?.InformationalVersion;
        if (version is null)
        {
            var asmVersion = typeof(BuilderApp).Assembly.GetName().Version ?? new Version();
            version = $"{asmVersion.Major}.{asmVersion.Minor}.{asmVersion.Build}";
        }


        var resolved = new Options();

        var _ = string.Empty;
        var options = new OptionSet
            {
                $"Copyright (C) {DateTime.Now.Year} Alexandre Mutel. All Rights Reserved",
                $"{exeName} - Version: {version}",
                _,
                $"Usage: {exeName} [options]+ solution_file.sln",
                _,
                "## Options",
                _,
                {"v|verbosity:", "Set verbosity.", v=> resolved.Verbosity = TryParseEnum<LoggerVerbosity>(v, "verbosity")},
                {"h|help", "Show this help.", v=> showHelp = true},
                _,
            };

        try
        {
            var arguments = options.Parse(args);

            if (showHelp)
            {
                options.WriteOptionDescriptions(Console.Error);
                return 0;
            }

            if (arguments.Count != 1)
            {
                throw new BuilderException("Expecting a path to a solution file");
            }

            var solutionPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, arguments[0]));
            if (!File.Exists(solutionPath))
            {
                throw new BuilderException($"The solution file {solutionPath} was not found!");
            }

            resolved.SolutionPath = solutionPath;

            var result = RunInternal(resolved);
            return result ? 0 : 1;
        }
        catch (Exception exception)
        {
            if (exception is OptionException || exception is BuilderException)
            {
                var backColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(exception.Message);
                Console.ForegroundColor = backColor;
                if (exception is BuilderException rocketException && rocketException.AdditionalText != null)
                {
                    Console.Error.WriteLine(rocketException.AdditionalText);
                }
                Console.Error.WriteLine("See --help for usage");
                return 1;
            }

            throw;
        }
    }

    private static TEnum TryParseEnum<TEnum>(string value, string optionName) where TEnum: struct
    {
        if (!Enum.TryParse<TEnum>(value, true, out var result))
        {
            throw new OptionException($"Invalid value `{value}` for option `{optionName}`. Valid values are: {string.Join(", ", Enum.GetNames(typeof(TEnum)).Select(x => x.ToLowerInvariant()))}", optionName);
        }

        return result;
    }

    private bool RunInternal(Options options)
    {
        var solutionPath = options.SolutionPath;
        var config = BuildConfiguration.Create(solutionPath);
        using var builder = new Builder(config);

        builder.Verbosity = options.Verbosity;

        var group = builder.CreateProjectGroup(ConfigurationHelper.Release());

        // Load the state
        var state = group.Load();

        if (state.Status == ProjectGroupStatus.NoChanges)
        {
            return true;
        }

        if (state.Status == ProjectGroupStatus.Restore)
        {
            state = group.Restore();
            // If we are stuck on restore, we had errors
            if (state.Status == ProjectGroupStatus.Restore)
            {
                return false;
            }
        }

        if (state.Status == ProjectGroupStatus.Build)
        {
#if DEBUG
            Console.WriteLine("Press enter to attach debugger");
            Console.ReadLine();
#endif

            group.Build();
        }

        return true;
    }

    private class Options
    {
        public Options()
        {
            Verbosity = LoggerVerbosity.Minimal;
            SolutionPath = string.Empty;
        }

        public LoggerVerbosity Verbosity { get; set; }
        
        public string SolutionPath { get; set; }
    }

    private class BuilderException : Exception
    {
        public BuilderException(string message) : base(message)
        {
        }

        public BuilderException(string message, string additionalText) : base(message)
        {
            AdditionalText = additionalText;
        }

        public string? AdditionalText { get; }
    }
}