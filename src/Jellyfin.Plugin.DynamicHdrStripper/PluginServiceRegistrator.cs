using MediaBrowser.Controller;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DynamicHdrStripper;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    private static readonly object InnerKey = new();
    public void RegisterServices(IServiceCollection services, IServerApplicationHost applicationHost)
    {
        if (applicationHost.ApplicationVersion != new Version(12, 4, 0, 0))
            throw new NotSupportedException("Dynamic HDR Stripper requires exactly Jellyfin Server 12.4.0.0.");
        var descriptors = services.Where(d => d.ServiceType == typeof(ITranscodeManager) && !d.IsKeyedService).ToArray();
        if (descriptors.Length != 1 || descriptors[0].Lifetime != ServiceLifetime.Singleton
            || descriptors[0].ImplementationType?.FullName != "MediaBrowser.MediaEncoding.Transcoding.TranscodeManager")
            throw new NotSupportedException("Unexpected transcode manager registration; refusing conflicting decoration.");
        var original = descriptors[0];
        services.Remove(original);
        // DI owns the original singleton and its disposal. The decorator never disposes it.
        services.Add(ServiceDescriptor.DescribeKeyed(typeof(ITranscodeManager), InnerKey, original.ImplementationType!, ServiceLifetime.Singleton));
        services.AddSingleton<SessionPolicies>();
        services.AddSingleton<ITranscodeManager>(sp => new HdrTranscodeManager(
            sp.GetRequiredKeyedService<ITranscodeManager>(InnerKey), sp.GetRequiredService<SessionPolicies>(),
            sp.GetRequiredService<ILogger<HdrTranscodeManager>>()));
        services.AddScoped<PlaybackFilter>();
        services.AddScoped<HlsFilter>();
        services.AddScoped<IEventConsumer<PlaybackStopEventArgs>, PlaybackStopConsumer>();
        services.PostConfigure<MvcOptions>(options => { options.Filters.AddService<PlaybackFilter>(); options.Filters.AddService<HlsFilter>(); });
    }
}
