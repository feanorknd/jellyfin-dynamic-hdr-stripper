using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Streaming;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Data;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DynamicHdrStripper;

public sealed class HdrTranscodeManager : ITranscodeManager
{
    private readonly ITranscodeManager _inner;
    private readonly SessionPolicies _policies;
    private readonly ILogger<HdrTranscodeManager> _logger;
    public HdrTranscodeManager(ITranscodeManager inner, SessionPolicies policies, ILogger<HdrTranscodeManager> logger)
    {
        _inner = inner; _policies = policies; _logger = logger;
        HdrLog.Info(logger, "Dynamic HDR Stripper: one original transcode manager decorated; ABI 12.4.0.0");
    }
    public Task<TranscodingJob> StartFfMpeg(StreamState state, string outputPath, string commandLineArguments, Guid userId,
        TranscodingJobType transcodingJobType, CancellationTokenSource cancellationTokenSource, string? workingDirectory = null)
    {
        var policy = _policies.Find(state.Request.PlaySessionId);
        if (policy is not null)
        {
            if (policy.UserId != userId || policy.ItemId != state.Request.Id || policy.DeviceId != state.Request.DeviceId
                || policy.SourceId != state.MediaSource.Id || transcodingJobType != TranscodingJobType.Hls
                || state.VideoStream?.Index != policy.VideoStreamIndex
                || !state.IsOutputVideo || !EncodingHelper.IsCopyCodec(state.OutputVideoCodec)
                || state.VideoRequest?.AllowVideoStreamCopy == false
                || state.User?.HasPermission(PermissionKind.EnablePlaybackRemuxing) != true
                || (!EncodingHelper.IsCopyCodec(state.OutputAudioCodec) && state.User?.HasPermission(PermissionKind.EnableAudioPlaybackTranscoding) != true)
                || HdrEligibility.Evaluate(state.VideoStream, policy.Mode, out _) != policy.Mode)
            {
                HdrLog.Warn(_logger, "Dynamic HDR Stripper: refusing FFmpeg launch; identity, permissions, source or video-copy invariant failed");
                throw new InvalidOperationException("HDR session rejected: identity, permissions, source or video-copy invariant failed.");
            }
            string rewritten;
            try { rewritten = FfmpegArguments.Rewrite(commandLineArguments, policy.Mode,
                policy.HadDolbyVision && !policy.Mode.HasFlag(StripMode.DolbyVision)); }
            catch (InvalidOperationException error)
            {
                HdrLog.Warn(_logger, "Dynamic HDR Stripper: refusing FFmpeg launch; reason={Reason}", error.Message);
                throw;
            }
            HdrLog.Info(_logger, "Dynamic HDR Stripper: mode={Mode}; original={Original}; modified={Modified}",
                policy.Mode, FfmpegArguments.Redact(commandLineArguments), FfmpegArguments.Redact(rewritten));
            commandLineArguments = rewritten;
        }
        return _inner.StartFfMpeg(state, outputPath, commandLineArguments, userId, transcodingJobType, cancellationTokenSource, workingDirectory);
    }
    public TranscodingJob? GetTranscodingJob(string playSessionId) => _inner.GetTranscodingJob(playSessionId);
    public TranscodingJob? GetTranscodingJob(string path, TranscodingJobType type) => _inner.GetTranscodingJob(path, type);
    public void PingTranscodingJob(string playSessionId, bool? isUserPaused) { _policies.Find(playSessionId); _inner.PingTranscodingJob(playSessionId, isUserPaused); }
    public async Task KillTranscodingJobs(string deviceId, string? playSessionId, Func<string, bool> deleteFiles)
    {
        await _inner.KillTranscodingJobs(deviceId, playSessionId, deleteFiles).ConfigureAwait(false);
        // Keep immutable policy for replay/seek requests until its idle expiry.
    }
    public void ReportTranscodingProgress(TranscodingJob job, StreamState state, TimeSpan? position, float? framerate, double? percent, long? bytes, int? bitrate)
        => _inner.ReportTranscodingProgress(job, state, position, framerate, percent, bytes, bitrate);
    public TranscodingJob? OnTranscodeBeginRequest(string path, TranscodingJobType type) => _inner.OnTranscodeBeginRequest(path, type);
    public void OnTranscodeEndRequest(TranscodingJob job) => _inner.OnTranscodeEndRequest(job);
    public ValueTask<IDisposable> LockAsync(string outputPath, CancellationToken cancellationToken) => _inner.LockAsync(outputPath, cancellationToken);
}
