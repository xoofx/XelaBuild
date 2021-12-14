using System.Collections.Generic;

namespace XelaBuild.Core.Helpers;

public static class ConfigurationHelper
{
    public static Dictionary<string, string> Debug()
    {
        return new Dictionary<string, string>()
        {
            { "Configuration", "Debug" },
            { "Platform", "AnyCPU" },
        };
    }

    public static Dictionary<string, string> Release()
    {
        return new Dictionary<string, string>()
        {
            { "Configuration", "Release" },
            { "Platform", "AnyCPU" },
        };
    }
}