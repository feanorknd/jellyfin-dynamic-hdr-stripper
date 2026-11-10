using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.DynamicHdrStripper;

public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths paths, IXmlSerializer serializer) : base(paths, serializer) => Instance = this;
    public static Plugin? Instance { get; private set; }
    public override Guid Id => Guid.Parse("e271ab40-03fd-4c83-a042-9a819a628da4");
    public override string Name => "Dynamic HDR Stripper";
    public override string Description => "Eliminación selectiva de HDR dinámico durante remux HEVC, sin recodificar vídeo.";
    public IEnumerable<PluginPageInfo> GetPages() => [new() { Name = "DynamicHdrStripper", EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html" }];
    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        var config = (PluginConfiguration)configuration;
        config.MaxConcurrentSessions = Math.Clamp(config.MaxConcurrentSessions, 1, 32);
        config.SessionIdleMinutes = Math.Clamp(config.SessionIdleMinutes, 5, 240);
        config.ProtectHdr10BaseLayer = true;
        config.IncludedClients ??= [];
        config.ExcludedClients ??= [];
        config.LoggingLevel = Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(config.LoggingLevel, true, out var level)
            ? level.ToString() : "Information";
        base.UpdateConfiguration(config);
    }
}
