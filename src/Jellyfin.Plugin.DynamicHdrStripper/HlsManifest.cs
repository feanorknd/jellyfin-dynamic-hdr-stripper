using Microsoft.AspNetCore.WebUtilities;

namespace Jellyfin.Plugin.DynamicHdrStripper;

public static class HlsManifest
{
    public static List<string> Attributes(string text)
    {
        var result = new List<string>();
        var quoted = false;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '"') quoted = !quoted;
            if (text[i] == ',' && !quoted) { result.Add(text[start..i]); start = i + 1; }
        }
        if (quoted) throw new InvalidOperationException("malformed-hls-attributes");
        result.Add(text[start..]);
        return result;
    }

    public static string Rewrite(string manifest, SessionPolicy policy)
    {
        var lines = manifest.Split('\n');
        var output = new List<string>();
        var variants = 0;
        const string prefix = "#EXT-X-STREAM-INF:";
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (!line.StartsWith(prefix, StringComparison.Ordinal)) { output.Add(line); continue; }
            if (++i >= lines.Length) throw new InvalidOperationException("missing-hls-variant-uri");
            var uri = lines[i].TrimEnd('\r');
            var query = QueryHelpers.ParseQuery(uri[(uri.IndexOf('?') + 1)..]);
            if (query.TryGetValue("AllowVideoStreamCopy", out var copy) && copy.ToString().Equals("false", StringComparison.OrdinalIgnoreCase)) continue;
            var attrs = Attributes(line[prefix.Length..]);
            var codecs = attrs.FirstOrDefault(a => a.StartsWith("CODECS=", StringComparison.Ordinal))?[7..].Trim('"').Split(',');
            var hevc = codecs?.FirstOrDefault(c => c.StartsWith("hvc1.", StringComparison.Ordinal) || c.StartsWith("hev1.", StringComparison.Ordinal));
            if (hevc is null) continue;
            attrs.RemoveAll(a => a.StartsWith("SUPPLEMENTAL-CODECS=", StringComparison.Ordinal) || a.StartsWith("VIDEO-RANGE=", StringComparison.Ordinal));
            attrs.Add("VIDEO-RANGE=PQ");
            if (policy.HadDolbyVision && !policy.Mode.HasFlag(StripMode.DolbyVision))
            {
                if (!policy.DolbyVisionLevel.HasValue) throw new InvalidOperationException("missing-dovi-level");
                attrs.Add($"SUPPLEMENTAL-CODECS=\"dvh1.08.{policy.DolbyVisionLevel.Value:D2}/db1p\"");
            }
            else if (policy.HadHdr10Plus && !policy.Mode.HasFlag(StripMode.Hdr10Plus))
                attrs.Add($"SUPPLEMENTAL-CODECS=\"{hevc}/cdm4\"");
            output.Add(prefix + string.Join(',', attrs));
            output.Add(uri);
            variants++;
        }
        if (variants == 0) throw new InvalidOperationException("no-hevc-copy-hls-variant");
        return string.Join('\n', output);
    }
}
