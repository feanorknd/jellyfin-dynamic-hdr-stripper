using System.Text.Json;
using Jellyfin.Api.Controllers;
using Jellyfin.Api.Extensions;
using Jellyfin.Api.Helpers;
using Jellyfin.Api.Models.MediaInfoDtos;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.MediaInfo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DynamicHdrStripper;

public sealed class PlaybackFilter(MediaInfoHelper helper, ILibraryManager library, IUserManager users,
    IDeviceManager devices, IMediaEncoder encoder, INetworkManager network, SessionPolicies policies,
    ITranscodeManager transcodeManager, ILogger<PlaybackFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var config = Plugin.Instance?.Configuration;
        if (context.ActionDescriptor is not ControllerActionDescriptor action
            || action.ControllerTypeInfo.AsType() != typeof(MediaInfoController)
            || action.MethodInfo.Name != nameof(MediaInfoController.GetPostedPlaybackInfo)
            || config?.Enabled != true || transcodeManager is not HdrTranscodeManager)
        { await next().ConfigureAwait(false); return; }

        var principal = context.HttpContext.User;
        var deviceId = principal.GetDeviceId();
        var requestedMode = ClientSelection.ResolveMode(config, principal.GetClient(), principal.GetDevice(), deviceId);
        if (string.IsNullOrEmpty(deviceId) || requestedMode == StripMode.Passthrough
            || (!config.AllowRemotePlayback && !network.IsInLocalNetwork(context.HttpContext.GetNormalizedRemoteIP())))
        { await next().ConfigureAwait(false); return; }

        var dto = context.ActionArguments.TryGetValue("playbackInfoDto", out var body) ? body as PlaybackInfoDto : null;
        T? Arg<T>(string key, T? fallback) where T : struct => context.ActionArguments.TryGetValue(key, out var value) && value is T typed ? typed : fallback;
        string? Str(string key, string? fallback) => context.ActionArguments.TryGetValue(key, out var value) ? value as string ?? fallback : fallback;
        var profile = dto?.DeviceProfile ?? devices.GetCapabilities(deviceId)?.DeviceProfile;
        var userId = principal.GetUserId();
        var user = users.GetUserById(userId);
        var itemId = (Guid)context.ActionArguments["itemId"]!;
        var item = library.GetItemById<BaseItem>(itemId, user);
        if (user is null || item is not Video || profile is null || !user.HasPermission(PermissionKind.EnablePlaybackRemuxing)
            || Arg("enableTranscoding", dto?.EnableTranscoding) == false
            || !string.IsNullOrEmpty(Str("liveStreamId", dto?.LiveStreamId)))
        { await next().ConfigureAwait(false); return; }

        var executed = await next().ConfigureAwait(false);
        if (executed.Result is not ObjectResult { Value: PlaybackInfoResponse response } || response.ErrorCode is not null
            || string.IsNullOrEmpty(response.PlaySessionId)) return;

        // Restrict only already advertised HLS HEVC profiles. Never invent client capability.
        var remuxProfile = Clone(profile);
        remuxProfile.TranscodingProfiles = remuxProfile.TranscodingProfiles
            .Where(p => p.Type == DlnaProfileType.Video && p.Protocol == MediaStreamProtocol.hls
                && p.VideoCodec.Split(',').Contains("hevc", StringComparer.OrdinalIgnoreCase))
            .Select(p => { var copy = new TranscodingProfile(p) { VideoCodec = "hevc" }; return copy; }).ToArray();
        if (remuxProfile.TranscodingProfiles.Length == 0) { HdrLog.Info(logger, "Dynamic HDR Stripper: skipped; client has no HEVC HLS profile"); return; }

        // One policy per PlaySessionId. Multi-source responses are left alone unless a source
        // was explicitly selected; Wholphin requests the selected MediaSourceId.
        var selectedId = Str("mediaSourceId", dto?.MediaSourceId);
        if (response.MediaSources.Count != 1 && string.IsNullOrEmpty(selectedId)) return;
        for (var index = 0; index < response.MediaSources.Count; index++)
        {
            var source = response.MediaSources[index];
            if (!string.IsNullOrEmpty(selectedId) && source.Id != selectedId) continue;
            if (source.IsRemote || source.RequiresOpening || source.IsInfiniteStream) continue;
            if (source.MediaStreams.Count(s => s.Type == MediaBrowser.Model.Entities.MediaStreamType.Video) != 1) continue;
            var video = source.MediaStreams.FirstOrDefault(s => s.Type == MediaBrowser.Model.Entities.MediaStreamType.Video);
            var mode = HdrEligibility.Evaluate(video, requestedMode, out var reason);
            HdrLog.Info(logger, "Dynamic HDR Stripper: decision={Reason}; mode={Mode}; dryRun={DryRun}", reason, mode, config.DryRun);
            if (mode == StripMode.Passthrough || config.DryRun) continue;
            if (Arg("allowVideoStreamCopy", dto?.AllowVideoStreamCopy) == false)
            {
                HdrLog.Warn(logger, "Dynamic HDR Stripper: eligible client requested video encoding; refusing SDR/encoding fallback");
                response.ErrorCode = PlaybackErrorCode.NoCompatibleStream;
                response.MediaSources = [];
                return;
            }
            if (!FfmpegCapabilities.Supports(encoder, mode))
            { HdrLog.Warn(logger, "Dynamic HDR Stripper: required FFmpeg bitstream filter unavailable; original negotiation retained"); continue; }

            var candidate = Clone(source);
            candidate.TranscodingUrl = null;
            helper.SetDeviceSpecificData(item, candidate, remuxProfile, principal,
                Arg("maxStreamingBitrate", dto?.MaxStreamingBitrate) ?? profile.MaxStreamingBitrate,
                Arg("startTimeTicks", dto?.StartTimeTicks) ?? 0, candidate.Id,
                Arg("audioStreamIndex", dto?.AudioStreamIndex), Arg("subtitleStreamIndex", dto?.SubtitleStreamIndex),
                Arg("maxAudioChannels", dto?.MaxAudioChannels), response.PlaySessionId, userId,
                false, true, true, true, Arg("allowAudioStreamCopy", dto?.AllowAudioStreamCopy) ?? true,
                false, context.HttpContext.GetNormalizedRemoteIP());
            if (string.IsNullOrEmpty(candidate.TranscodingUrl) || candidate.SupportsDirectPlay) continue;
            var url = candidate.TranscodingUrl;
            var query = QueryHelpers.ParseQuery(url[(url.IndexOf('?') + 1)..]);
            if (candidate.DefaultSubtitleStreamIndex >= 0 && (dto?.AlwaysBurnInSubtitleWhenTranscoding == true
                || (query.TryGetValue("SubtitleMethod", out var method) && method.ToString().Equals("Encode", StringComparison.OrdinalIgnoreCase))))
            {
                HdrLog.Warn(logger, "Dynamic HDR Stripper: selected subtitles require video encoding; refusing transformation");
                response.ErrorCode = PlaybackErrorCode.NoCompatibleStream;
                response.MediaSources = [];
                return;
            }
            if (!policies.TryAdd(new SessionPolicy
                {
                    SessionId = response.PlaySessionId, DeviceId = deviceId, UserId = userId,
                    ItemId = itemId, SourceId = source.Id, Mode = mode,
                    HadDolbyVision = video!.DvProfile.HasValue, HadHdr10Plus = video.Hdr10PlusPresentFlag == true,
                    DolbyVisionLevel = video.DvLevel, VideoStreamIndex = video.Index
                }, config))
            { HdrLog.Info(logger, "Dynamic HDR Stripper: session limit reached; original negotiation retained"); continue; }
            candidate.TranscodingUrl = QueryHelpers.AddQueryString(url, "dhdr", "1");
            var updatedSources = response.MediaSources.ToArray();
            updatedSources[index] = candidate;
            response.MediaSources = updatedSources;
            break;
        }
    }

    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value))!;
}
