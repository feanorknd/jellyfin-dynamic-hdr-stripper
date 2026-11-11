using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.DynamicHdrStripper;

public static class ClientSelection
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(50);

    public static void Validate(ClientConfiguration rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (string.IsNullOrWhiteSpace(rule.Pattern) || rule.Pattern.Length > 256)
            throw new ArgumentException("Each client needs a word or regular expression of 1 to 256 characters.");
        if (rule.Mode is not (ClientStripMode.DolbyVision or ClientStripMode.Both))
            throw new ArgumentException("The specific mode must remove DV or remove DV and HDR10+.");
        _ = new Regex(rule.Pattern, Options, MatchTimeout);
    }

    public static StripMode ResolveMode(PluginConfiguration config, string? client, string? device, string? deviceId)
    {
        foreach (var rule in config.ClientConfigurations ?? [])
        {
            if (rule is null || string.IsNullOrWhiteSpace(rule.Pattern) || rule.Pattern.Length > 256
                || rule.Mode is not (ClientStripMode.DolbyVision or ClientStripMode.Both)) continue;
            try
            {
                if (Matches(client, rule.Pattern) || Matches(device, rule.Pattern) || Matches(deviceId, rule.Pattern))
                    return (StripMode)rule.Mode;
            }
            // A malformed hand-edited configuration or expensive pattern must not break playback.
            catch (ArgumentException) { }
            catch (RegexMatchTimeoutException) { }
        }
        return config.ApplyGlobally ? config.Mode : StripMode.Passthrough;
    }

    private static bool Matches(string? value, string pattern)
        => !string.IsNullOrEmpty(value) && Regex.IsMatch(value, pattern, Options, MatchTimeout);
}
