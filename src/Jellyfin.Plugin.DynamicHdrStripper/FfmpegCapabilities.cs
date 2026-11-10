using MediaBrowser.Controller.MediaEncoding;

namespace Jellyfin.Plugin.DynamicHdrStripper;

public static class FfmpegCapabilities
{
    public static bool Supports(IMediaEncoder encoder, StripMode mode)
        => (!mode.HasFlag(StripMode.DolbyVision) || encoder.SupportsBitStreamFilterWithOption(BitStreamFilterOptionType.DoviRpuStrip))
            && (!mode.HasFlag(StripMode.Hdr10Plus) || encoder.SupportsBitStreamFilterWithOption(BitStreamFilterOptionType.HevcMetadataRemoveHdr10Plus));
}
