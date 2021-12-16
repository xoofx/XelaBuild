using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace XelaBuild.Core;

public partial class Builder
{
    private readonly Logger _log;

    private static readonly List<ConsoleRowHighlightingRule> DefaultColors = new()
    {
        new ConsoleRowHighlightingRule("level == LogLevel.Fatal", ConsoleOutputColor.Magenta, ConsoleOutputColor.NoChange),
        new ConsoleRowHighlightingRule("level == LogLevel.Error", ConsoleOutputColor.Red, ConsoleOutputColor.NoChange),
        new ConsoleRowHighlightingRule("level == LogLevel.Warn", ConsoleOutputColor.Yellow, ConsoleOutputColor.NoChange),
        new ConsoleRowHighlightingRule("level == LogLevel.Info", ConsoleOutputColor.Green, ConsoleOutputColor.NoChange),
        new ConsoleRowHighlightingRule("level == LogLevel.Debug", ConsoleOutputColor.Gray, ConsoleOutputColor.NoChange),
        new ConsoleRowHighlightingRule("level == LogLevel.Trace", ConsoleOutputColor.DarkGray, ConsoleOutputColor.NoChange),
    };

    private static void InitializeLoggers(out Logger log)
    {
        var config = new LoggingConfiguration();
        var consoleTarget = new ColoredConsoleTarget();

        consoleTarget.EnableAnsiOutput = true;
        consoleTarget.RowHighlightingRules.Clear();
        foreach (var consoleRowHighlightingRule in DefaultColors)
        {
            consoleTarget.RowHighlightingRules.Add(consoleRowHighlightingRule);
        }

        consoleTarget.Layout = "[${counter:padding=3:padCharacter=0}]: ${longdate}: ${level:lowercase=true:padding=-5}: ${message}";
        config.AddTarget("console", consoleTarget);
        config.AddRule(LogLevel.Debug, LogLevel.Fatal, consoleTarget);
        LogManager.Configuration = config;

        log = LogManager.GetCurrentClassLogger();
    }

    public void Trace(string message)
    {
        _log.Trace(message);
    }

    public void Debug(string message)
    {
        _log.Debug(message);
    }

    public void Info(string message)
    {
        _log.Info(message);
    }

    public void Warning(string message)
    {
        _log.Warn(message);
    }

    public void Error(string message)
    {
        _log.Error(message);
    }
    public void Fatal(string message)
    {
        _log.Fatal(message);
    }
}