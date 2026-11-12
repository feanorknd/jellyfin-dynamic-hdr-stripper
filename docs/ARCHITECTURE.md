# Architecture and references

Target: Jellyfin Server tag [`v12.4`](https://github.com/jellyfin/jellyfin/tree/v12.4),
commit `83d0d0e7f76e23e2e085c8ff67d28b772d35967d`, .NET 10.

## Client policy and negotiation

`ClientSelection` searches ordered regular expressions independently against the
authenticated application name, device name and DeviceId, using case-insensitive,
culture-invariant matching with a 50 ms timeout. The first matching specific
configuration selects DV-only removal, DV+HDR10+ removal, or Passthrough. New
client configurations default to Passthrough, which returns immediately to normal
Jellyfin negotiation without reserving a plugin session or modifying HDR metadata,
even when global removal is enabled or a later rule requests removal.
Only unmatched clients use the general mode, and only when global application is enabled. Legacy included
and excluded lists are no longer read. Enabled, Dry Run, network restrictions and
video eligibility still apply.

`PlaybackFilter` intercepts the typed response from
`MediaInfoController.GetPostedPlaybackInfo`. It uses authenticated sources and
`MediaInfoHelper.SetDeviceSpecificData` to renegotiate eligible playback. It
preserves advertised client profiles and user restrictions; it does not fabricate
TranscodingUrl values or disable DirectPlay globally. Specific rules can select
a mode even when the general mode is passthrough.

Wholphin 1.1.1, `PlaybackViewModel.changeStreams`, consumes TranscodingUrl when
SupportsDirectPlay=false. Physical display behavior requires separate validation.

## Execution

`PluginServiceRegistrator` moves the existing ITranscodeManager singleton
descriptor to a private keyed registration and exposes a decorator. The original
instance continues managing FFmpeg processes, cache and jobs. No second DI
provider or copied TranscodeManager is created.

`HdrTranscodeManager` checks session, user, device, source, stream, permissions and
video-copy output before adding fixed bitstream filters. Other operations are
forwarded unchanged. `FfmpegArguments` changes only identified tokens, preserves
other arguments, and rejects ambiguous or incompatible commands.

Native `EncodingHelper.ShouldRemoveDynamicHdrMetadata` and `GetBitStreamArgs`
can add filters themselves; the composer reconciles only known filter chains.

## HLS and cache

`HlsFilter` and `HlsManifest` correct supplemental CODECS and VIDEO-RANGE for
affected manifests. Output stays HEVC/PQ. Preserved DV retains its fMP4
configuration through the native muxer setting `-strict -2`.

`StreamingHelpers.GetOutputFilePath` includes MediaPath, UserAgent, DeviceId and
PlaySessionId in its hash. `SessionPolicies` freezes the selected mode per session
to avoid changing policy between segments. Stop frees the reservation; abandoned
policies expire when reservations are cleaned up. At the concurrency limit, new
playbacks retain normal Jellyfin negotiation.

## Dashboard and image

The configuration page uses Jellyfin Web's `<html lang>` value, which reflects
the user's interface-language setting, before falling back to the browser locale.
English and Spanish strings are built in; unsupported locales use English.
User-entered regex patterns are assigned as input values rather than HTML.

The package includes `logo.svg` and declares it with `imagePath` in `meta.json`.
The catalog's `imageUrl` provides the same logo for catalog cards and native
installation. All documentation, catalog metadata and release notes are English.

## Architectural references

- https://github.com/jellyfin/jellyfin-plugin-template
- https://github.com/calibrae/jellyfin-anemone
- https://github.com/CrunchyMonkies/jellyops-plugin-jellycode
- https://github.com/upchui/Jellyfin-DirectPlayForce
- https://github.com/SKProCH/JellyfinStreamGenerator

These informed the investigation of DI, MVC filters and streaming; their
implementations were not copied as replacements for the original manager.
There are no global FFmpeg wrappers, Server patches or bundled FFmpeg binaries.
