# Jellyfin Dynamic HDR Stripper

Some players do not detect HDR correctly when a video file includes Dolby Vision,
which can prevent a movie from being displayed correctly. This plugin exists to
work around that problem by removing Dolby Vision, and optionally HDR10+, during
playback. To do this, Jellyfin switches the affected playback to **HLS
transcoding/remux**: the video is **copied without re-encoding**, keeping its HDR10
base layer. The original file is never modified.

[Download v1.2.0](https://github.com/feanorknd/jellyfin-dynamic-hdr-stripper/releases/tag/v1.2.0)
· [Plugin repository manifest](https://raw.githubusercontent.com/feanorknd/jellyfin-dynamic-hdr-stripper/main/manifest.json)
· [Compatibility and validation](docs/COMPATIBILITY.md)
· [Architecture](docs/ARCHITECTURE.md)

## Compatibility

| Component | Target |
|---|---|
| Jellyfin **Server** | **12.4.0.0**, also checked when loading the plugin |
| Runtime | .NET 10 |
| Container | Official `jellyfin/jellyfin:12.4` image |
| FFmpeg | `8.1.3-Jellyfin`, included in the official image |
| Delivery | HEVC HLS with MPEG-TS or fMP4 segments |

The Jellyfin Web version does not determine compatibility. The catalog declares
`targetAbi: 12.4.0.0`; the plugin requires exactly that Server version.
See [validation results](docs/COMPATIBILITY.md) for the tested playback paths and
the physical-device checks still pending.

## Install or update from Jellyfin

1. Open **Dashboard → Plugins → Repositories → Add**.
2. Enter the name `Dynamic HDR Stripper` and this URL:

   ```text
   https://raw.githubusercontent.com/feanorknd/jellyfin-dynamic-hdr-stripper/main/manifest.json
   ```

3. Save, open the **Catalog**, and install **Dynamic HDR Stripper**.
4. Restart Jellyfin and open the plugin configuration.

If you already use this repository, v1.2.0 is published as **1.2.0.0**. Refresh
the plugin catalog or run Jellyfin's plugin update task, install the update, and
restart Jellyfin. Existing client rules keep their selected modes. New client
configurations default to **Passthrough**.

**Upgrading from 1.0:** included/excluded client lists have been removed and are
no longer used. Recreate your desired client selections using the new specific
configurations. The remaining general settings are preserved.

New installations start disabled, with Dry Run enabled, global application off,
and no specific client configurations.

### Manual installation

Download `DynamicHDRStripper_1.2.0.zip` and `SHA256SUMS` from
[Releases](https://github.com/feanorknd/jellyfin-dynamic-hdr-stripper/releases).
With Jellyfin stopped, extract the ZIP under:

```text
/config/plugins/Dynamic HDR Stripper_1.2.0.0/
├── Jellyfin.Plugin.DynamicHdrStripper.dll
├── meta.json
├── logo.svg
├── README.md
├── LICENSE
└── docs/
```

The DLL and `meta.json` must be directly in that directory. Start Jellyfin after
installation. No replacement FFmpeg or custom Jellyfin image is needed.

## Client configuration

**Apply globally** uses the general removal settings for eligible clients that
do not match a specific configuration. The two general checkboxes allow keeping
everything, removing DV only, removing HDR10+ only, or removing both.

Use **＋ Add client** to create a specific configuration:

- Enter one **word or regular expression**, such as `xiaomi`, `wholphin`, or
  `wholphin|android`.
- The expression is searched **case-insensitively** in the application name,
  device name, and device identifier (`DeviceId`). A match in **any** field is enough.
- Select **Remove Dolby Vision**, **Remove Dolby Vision and HDR10+**, or
  **Passthrough**. Passthrough is selected by default for new clients.
- Use **Remove** to delete a configuration and the arrows to change its priority.
- Save your changes.

The **first matching specific configuration overrides the general mode**, even
when global application is off or the general checkboxes are both unchecked.
For example, a DV-only client rule preserves HDR10+ even if the general mode
removes both. A **Passthrough** rule leaves normal Jellyfin playback unchanged,
preserving both DV and HDR10+ and overriding global removal or any later matching
rule. Use it to exempt a compatible player from global processing. It does not
force DirectPlay if Jellyfin needs remuxing or transcoding for another reason.
With no matching rule and global application off, playback stays unchanged.
Removal modes still follow the plugin's eligibility and network settings.

Expressions are validated on save. Each is limited to 256 characters and matching
has a 50 ms timeout. Empty expressions are not accepted. Use simple expressions
compatible with JavaScript and .NET regular expressions; enter them without `/…/`
delimiters. The menu follows Jellyfin's interface language: English and Spanish
are available, with English as the fallback for other languages.

### First playback

1. Add a specific configuration, for example `wholphin`, with the desired mode.
2. Enable the plugin and keep **Dry Run** on to check its logged decisions.
3. Disable Dry Run, save, stop playback and start it again.
4. Check HDR on the display, audio, subtitles and seeking.

## Advanced settings

- **Also apply to playback from outside the local network:** allows processing
  for players Jellyfin classifies as remote, such as Internet connections. When
  off, only local players are processed. Jellyfin's network, proxy and VPN
  settings determine this classification. This does not enable remote media sources.
- **Maximum simultaneously managed playbacks:** defaults to 2. Includes negotiated
  sessions not yet playing. At the limit, new playbacks use normal Jellyfin
  negotiation without plugin intervention.
- **Release inactive sessions after:** defaults to 30 minutes. A session stores
  the chosen mode for one playback; Stop releases it, and the timeout reclaims
  abandoned sessions. This is not a maximum movie duration.
- **Logging:** controls the plugin's minimum log level. Its own diagnostics redact
  paths and tokens; native Jellyfin/FFmpeg logs may still include paths.

The selected mode is fixed for each playback. Save, stop and restart playback to
apply changes. Jellyfin manages FFmpeg processes, segments and cache cleanup.

## Supported videos

The plugin requires HEVC Main 10, 10-bit PQ/SMPTE2084, BT.2020 and BT.2020nc,
with a verified HDR10 base. Supported dynamic metadata:

- Dolby Vision Profile 8, compatibility ID 1, RPU and base present, no enhancement layer.
- HDR10+ with a verified HDR10 base.

Pure HDR10, SDR, DV P5/P7, P8.2, HLG and incomplete metadata are skipped. Profile 5
is not converted to HDR10. A file source, a client advertising HEVC HLS and remux
permission are required. Audio permissions are respected; video encoding permission
is not needed. Burn-in subtitles, remote media sources and live TV are not supported.

The fixed filters are `dovi_rpu=strip=1` and
`hevc_metadata=remove_hdr10plus=1`. Video payloads are copied. A transformed
session cannot silently fall back to video re-encoding or SDR.

## Build

Python 3 and Docker with Linux containers are required:

```bash
python3 scripts/build.py
```

This extracts build references from the pinned official image, builds with the
.NET 10 SDK in a separate container, and produces the ZIP, `SHA256SUMS`, and
`dist/manifest.json`. It does not deploy the plugin. Server assemblies are never
included in the package.

With a local .NET 10 SDK and official Server assemblies:

```bash
dotnet build src/Jellyfin.Plugin.DynamicHdrStripper -c Release \
  -p:HostAssemblies=/path/to/official/assemblies
python3 scripts/build.py --package-only
```

## Disable, uninstall or roll back

- Disable the plugin and start a new playback to stop applying new transformations.
- Uninstall through Jellyfin's plugin dashboard and restart the server.
- For manual rollback, stop Jellyfin and restore the previous compatible plugin
  folder. Keep only the intended version active.

No media files or Jellyfin databases are modified. License: **GPL-2.0-or-later**;
see [LICENSE](LICENSE) and the [full license](docs/GPL-2.0.txt).
