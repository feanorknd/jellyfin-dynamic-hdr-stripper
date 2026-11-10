using System.Collections.Concurrent;

namespace Jellyfin.Plugin.DynamicHdrStripper;

public sealed class SessionPolicy
{
    public required string SessionId { get; init; }
    public required string DeviceId { get; init; }
    public required Guid UserId { get; init; }
    public required Guid ItemId { get; init; }
    public required string SourceId { get; init; }
    public required StripMode Mode { get; init; }
    public required bool HadDolbyVision { get; init; }
    public required bool HadHdr10Plus { get; init; }
    public int? DolbyVisionLevel { get; init; }
    public int VideoStreamIndex { get; init; }
    public DateTime LastAccessUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SessionPolicies
{
    private readonly ConcurrentDictionary<string, SessionPolicy> _policies = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    public bool TryAdd(SessionPolicy policy, PluginConfiguration config)
    {
        lock (_gate)
        {
            foreach (var pair in _policies)
                if (DateTime.UtcNow - pair.Value.LastAccessUtc > TimeSpan.FromMinutes(Math.Clamp(config.SessionIdleMinutes, 5, 240)))
                    _policies.TryRemove(pair.Key, out _);
            if (_policies.Count >= Math.Clamp(config.MaxConcurrentSessions, 1, 32)) return false;
            return _policies.TryAdd(policy.SessionId, policy);
        }
    }
    public SessionPolicy? Find(string? id)
    {
        if (id is null || !_policies.TryGetValue(id, out var policy)) return null;
        policy.LastAccessUtc = DateTime.UtcNow;
        return policy;
    }
    public void Remove(string? id) { if (id is not null) _policies.TryRemove(id, out _); }
}
