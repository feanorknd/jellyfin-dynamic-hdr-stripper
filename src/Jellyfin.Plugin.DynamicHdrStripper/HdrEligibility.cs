using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.DynamicHdrStripper;

public static class HdrEligibility
{
    public static StripMode Evaluate(MediaStream? video, StripMode requested, out string reason)
    {
        reason = "eligible";
        if (video is null || video.Type != MediaStreamType.Video || !string.Equals(video.Codec, "hevc", StringComparison.OrdinalIgnoreCase))
            return Skip("not-hevc", out reason);
        if (video.BitDepth != 10 || !string.Equals(video.Profile?.Replace(" ", "", StringComparison.Ordinal), "Main10", StringComparison.OrdinalIgnoreCase))
            return Skip("not-main10", out reason);
        if (!string.Equals(video.ColorTransfer, "smpte2084", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(video.ColorPrimaries, "bt2020", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(video.ColorSpace, "bt2020nc", StringComparison.OrdinalIgnoreCase))
            return Skip("unverified-hdr10-base", out reason);

        var hasDv = video.DvProfile.HasValue || video.RpuPresentFlag == 1;
        if (hasDv && (video.DvProfile != 8 || video.DvBlSignalCompatibilityId != 1
            || video.BlPresentFlag != 1 || video.ElPresentFlag != 0 || video.RpuPresentFlag != 1))
            return Skip("unsupported-dovi-profile-or-layers", out reason);

        var present = (hasDv ? StripMode.DolbyVision : 0)
            | (video.Hdr10PlusPresentFlag == true ? StripMode.Hdr10Plus : 0);
        var mode = requested & present;
        if (mode == StripMode.Passthrough) reason = "no-requested-dynamic-metadata";
        return mode;
    }

    private static StripMode Skip(string value, out string reason) { reason = value; return StripMode.Passthrough; }
}
