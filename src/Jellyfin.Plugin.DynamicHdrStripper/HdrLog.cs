using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DynamicHdrStripper;

public static class HdrLog
{
    private static bool Allows(LogLevel level)
        => !Enum.TryParse<LogLevel>(Plugin.Instance?.Configuration.LoggingLevel, true, out var minimum) || level >= minimum;
    public static void Info(ILogger logger, string template, params object?[] values)
    { if (Allows(LogLevel.Information)) logger.Log(LogLevel.Information, template, values); }
    public static void Warn(ILogger logger, string template, params object?[] values)
    { if (Allows(LogLevel.Warning)) logger.Log(LogLevel.Warning, template, values); }
}
