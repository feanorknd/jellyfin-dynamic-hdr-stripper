using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.DynamicHdrStripper;

public sealed class PlaybackStopConsumer(SessionPolicies policies) : IEventConsumer<PlaybackStopEventArgs>
{
    public Task OnEvent(PlaybackStopEventArgs args)
    {
        var policy = policies.Find(args.PlaySessionId);
        if (policy?.DeviceId == args.DeviceId && args.Users.Any(user => user.Id == policy.UserId))
            policies.Remove(args.PlaySessionId);
        return Task.CompletedTask;
    }
}
