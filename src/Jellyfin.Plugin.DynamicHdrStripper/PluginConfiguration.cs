using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.DynamicHdrStripper;

[Flags]
public enum StripMode { Passthrough = 0, DolbyVision = 1, Hdr10Plus = 2, Both = 3 }

public enum ClientStripMode { DolbyVision = 1, Both = 3 }

public sealed class ClientConfiguration
{
    public string Pattern { get; set; } = "";
    public ClientStripMode Mode { get; set; } = ClientStripMode.DolbyVision;
}

public sealed class PluginConfiguration : BasePluginConfiguration
{
    public bool Enabled { get; set; }
    public bool DryRun { get; set; } = true;
    public bool StripDolbyVision { get; set; } = true;
    public bool StripHdr10Plus { get; set; } = true;
    public bool ApplyGlobally { get; set; }
    public ClientConfiguration[] ClientConfigurations { get; set; } = [];
    public bool AllowRemotePlayback { get; set; }
    public int MaxConcurrentSessions { get; set; } = 2;
    public int SessionIdleMinutes { get; set; } = 30;
    public string LoggingLevel { get; set; } = "Information";
    // Mandatory invariant: this release never transforms a stream without verified HDR10 BL.
    public bool ProtectHdr10BaseLayer { get; set; } = true;
    public StripMode Mode => (StripDolbyVision ? StripMode.DolbyVision : 0) | (StripHdr10Plus ? StripMode.Hdr10Plus : 0);
}
