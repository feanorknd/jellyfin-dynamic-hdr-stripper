using System.Text;

namespace Jellyfin.Plugin.DynamicHdrStripper;

// Edits only the bsf token (or inserts immediately after the video codec).
// All other bytes, including quotes and private input/output paths, are retained.
public static class FfmpegArguments
{
    public sealed record Token(int Start, int End, string Value);

    public static List<Token> Tokenize(string text)
    {
        var result = new List<Token>();
        for (var i = 0; i < text.Length;)
        {
            if (char.IsWhiteSpace(text[i])) { i++; continue; }
            var start = i;
            var value = new StringBuilder();
            char quote = '\0';
            while (i < text.Length && (quote != '\0' || !char.IsWhiteSpace(text[i])))
            {
                var c = text[i++];
                if (c == '\\' && i < text.Length && text[i] == '"') { value.Append(text[i++]); continue; }
                if (c is '\'' or '"')
                {
                    if (quote == '\0') { quote = c; continue; }
                    if (quote == c) { quote = '\0'; continue; }
                }
                value.Append(c);
            }
            if (quote != '\0') throw new InvalidOperationException("unbalanced-arguments");
            result.Add(new Token(start, i, value.ToString()));
        }
        return result;
    }

    public static string Rewrite(string arguments, StripMode mode, bool retainDoviConfiguration = false)
    {
        if (mode == StripMode.Passthrough) return arguments;
        var tokens = Tokenize(arguments);
        Token? codecValue = null;
        Token? bsfValue = null;
        for (var i = 0; i < tokens.Count; i++)
        {
            var option = tokens[i].Value;
            if (option is "-vf" or "-filter:v" or "-filter:v:0" or "-filter_complex") throw new InvalidOperationException("video-filter-conflict");
            if (option is "-c" or "-codec" || (option.StartsWith("-bsf:v:", StringComparison.Ordinal) && option != "-bsf:v:0"))
                throw new InvalidOperationException("ambiguous-video-options");
            if (option is "-codec:v:0" or "-codec:v" or "-c:v" or "-c:v:0" or "-vcodec")
            {
                if (codecValue is not null || ++i >= tokens.Count || tokens[i].Value != "copy") throw new InvalidOperationException("video-not-copy");
                codecValue = tokens[i];
            }
            else if (option is "-bsf:v" or "-bsf:v:0")
            {
                if (bsfValue is not null || ++i >= tokens.Count) throw new InvalidOperationException("ambiguous-bsf");
                bsfValue = tokens[i];
            }
            else if (option.StartsWith("-codec:v:", StringComparison.Ordinal) || option.StartsWith("-c:v:", StringComparison.Ordinal))
                throw new InvalidOperationException("multiple-output-video-streams");
        }
        if (codecValue is null) throw new InvalidOperationException("missing-video-copy");
        var filters = new List<string>();
        if (bsfValue is not null)
        {
            foreach (var filter in bsfValue.Value.Split(','))
            {
                if (filter == "hevc_mp4toannexb") { if (!filters.Contains(filter)) filters.Add(filter); }
                else if (filter == "dovi_rpu=strip=1") { /* reconciled below */ }
                else if (filter.StartsWith("hevc_metadata=", StringComparison.Ordinal)
                    && filter[14..].Split(':').All(x => x is "remove_dovi=1" or "remove_hdr10plus=1")) { }
                else throw new InvalidOperationException("unknown-or-incompatible-bsf");
            }
        }
        if (mode.HasFlag(StripMode.DolbyVision)) filters.Add("dovi_rpu=strip=1");
        if (mode.HasFlag(StripMode.Hdr10Plus)) filters.Add("hevc_metadata=remove_hdr10plus=1");
        var chain = string.Join(',', filters);
        var rewritten = bsfValue is not null
            ? string.Concat(arguments.AsSpan(0, bsfValue.Start), chain, arguments.AsSpan(bsfValue.End))
            : arguments.Insert(codecValue.End, " -bsf:v:0 " + chain);
        if (!retainDoviConfiguration) return rewritten;
        var outputTokens = Tokenize(rewritten);
        var strictPresent = false;
        for (var i = 0; i < outputTokens.Count; i++)
        {
            if (outputTokens[i].Value is "-strict" or "-strict:v" or "-strict:v:0")
            {
                var appliesToMuxer = outputTokens[i].Value == "-strict";
                if (++i >= outputTokens.Count || outputTokens[i].Value != "-2") throw new InvalidOperationException("incompatible-dovi-mux-strictness");
                strictPresent |= appliesToMuxer;
            }
        }
        // Native DynamicHlsController uses -strict -2 for Dolby Vision remuxing.
        // Apply native muxer strictness when preserving DV; otherwise movenc drops
        // DOVI_CONF if the client profile advertised only the HDR10 fallback.
        var videoCodecIndex = outputTokens.FindIndex(t => t.Value is "-codec:v:0" or "-codec:v" or "-c:v" or "-c:v:0" or "-vcodec");
        return strictPresent ? rewritten : rewritten.Insert(outputTokens[videoCodecIndex + 1].End, " -strict -2");
    }

    public static string Redact(string arguments)
    {
        var tokens = Tokenize(arguments);
        var safe = new HashSet<string> { "-codec:v:0", "-codec:v", "-c:v", "-c:v:0", "-vcodec", "-bsf:v", "-bsf:v:0" };
        return string.Join(' ', tokens.Select((t, i) => safe.Contains(t.Value)
            ? t.Value : i > 0 && safe.Contains(tokens[i - 1].Value) ? t.Value : "<redacted>"));
    }
}
