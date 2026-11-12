# Compatibility and validation

## Exact target

- Jellyfin Server **12.4.0.0**, .NET 10, Linux x64.
- Server tag `v12.4`, commit `83d0d0e7f76e23e2e085c8ff67d28b772d35967d`.
- Official `jellyfin/jellyfin:12.4` image, pinned by digest in the build script.
- Original FFmpeg **8.1.3-Jellyfin**.

The catalog ABI is a minimum version from Jellyfin's perspective. The DLL also
checks for exactly Server 12.4.0.0. Other Server versions are not declared compatible.

## Playback validation

Contract and integration tests use an isolated official Server and a real HEVC
Main 10 hybrid clip with DV P8/compatibility 1, HDR10+, an HDR10 base and no
enhancement layer. Private samples and laboratory state are not distributed.

| Requested output | MPEG-TS | fMP4 |
|---|---|---|
| Remove DV, preserve HDR10+ | Verified | Verified |
| Remove HDR10+, preserve DV | Verified | Verified, including DV init configuration |
| Remove both | Verified | Verified |

Checks cover:

- Actual RPU presence/absence using HEVC Annex B extraction.
- Actual HDR10+ presence/absence in decoded frame side data.
- SHA256-identical VCL payloads, confirming video is not re-encoded.
- Preserved resolution, 10-bit depth, PQ, BT.2020 and BT.2020nc.
- Preserved mastering-display and content-light metadata.
- HLS signaling and fMP4 initialization consistent with the selected output.
- Pure HDR10 and SDR left unchanged; selective processing of HDR10+ sources.
- Disabled plugin, Dry Run and unmatched clients retaining original playback.
- Isolated concurrent frozen policies, seeking, pause/resume APIs and E-AC-3/Atmos.
- External delivery of embedded SubRip and external SRT subtitles.
- User permissions, concurrency, Stop cleanup and no orphan FFmpeg process.
- An injected real FFmpeg failure propagating without a silent SDR fallback or retry.

Version 1.1 adds contract coverage for case-insensitive matches in each identity
field, regex syntax and timeouts, first-match priority, specific overrides of the
general mode, global fallback and specific selection when the general mode is
passthrough. The administrative page and catalog upgrade path are also checked.

Version 1.2 adds Passthrough as the default for new specific client configurations.
Regression checks cover overriding each global removal mode, first-match priority
over later removal rules, and preserving the global fallback for unmatched clients.
The configuration API and dashboard are checked for saving and reloading all three
client modes, including preservation of existing removal selections.

Only extracted segments are analyzed, not every frame of a full movie. HDR10,
HDR10+ and DV-only fixtures derive from the hybrid clip and are not an independent corpus.

## Remaining physical and extended checks

1. Wholphin 1.1.1 / ExoPlayer on Xiaomi Mi Box S, including actual HDR activation
   on a Hisense display. Metadata analysis alone does not prove display behavior.
2. Independent real Profile 5 and Profile 7 samples; their exclusion is covered
   by contract tests.
3. A sample of at least five minutes for native throttling and segment-cleaner
   conditions; the available main clip is three minutes long.
4. Sustained load, abandonment without Stop, restarts during streaming and
   coexistence with other PlaybackInfo/HLS/DI plugins.

## Current exclusions

DV P5/P7, P8.2, HLG, incomplete HDR10 signaling, enhancement layers, remote media
sources/live TV, multiple video tracks and subtitle burn-in. Multi-source
responses require an explicitly selected source.

The plugin does not convert HDR to SDR or modify original media files.
