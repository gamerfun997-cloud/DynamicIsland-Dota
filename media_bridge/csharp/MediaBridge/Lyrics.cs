using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MediaBridge;

public static partial class Lyrics
{
    private const string Api = "https://lrclib.net/api/";
    private static readonly HttpClient Http = CreateClient();
    private static readonly Dictionary<string, string> Cache = new();
    private static readonly Dictionary<string, Task<string>> Pending = new();
    private static readonly object Sync = new();

    [GeneratedRegex(@"\[(\d+):(\d+)(?:[.:](\d+))?\]")]
    private static partial Regex StampRegex();

    [GeneratedRegex(@"\s*[\(\[][^\)\]]*[\)\]]|\s+-\s+.*$")]
    private static partial Regex NoiseRegex();

    [GeneratedRegex(@"<(\d+):(\d+)(?:[.:](\d+))?>")]
    private static partial Regex WordRegex();

    private static long Ms(Match m)
    {
        long min = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        long sec = long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        string f = m.Groups[3].Value;
        long frac = f == "" ? 0 : long.Parse(f.Length > 3 ? f[..3] : f.PadRight(3, '0'), CultureInfo.InvariantCulture);
        return min * 60000 + sec * 1000 + frac;
    }

    private static (string text, string words) Words(string raw)
    {
        var tags = WordRegex().Matches(raw);
        if (tags.Count == 0) return (raw.Trim(), "");
        var clean = new StringBuilder();
        var marks = new List<string>();
        int at = 0;
        foreach (Match m in tags)
        {
            clean.Append(raw, at, m.Index - at);
            at = m.Index + m.Length;
            string prefix = clean.ToString();
            int count = prefix.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            bool inWord = prefix.Length > 0 && !char.IsWhiteSpace(prefix[^1]);
            int idx = inWord ? Math.Max(0, count - 1) : count;
            marks.Add(idx.ToString(CultureInfo.InvariantCulture) + ":" + Ms(m).ToString(CultureInfo.InvariantCulture));
        }
        clean.Append(raw, at, raw.Length - at);
        string text = clean.ToString();
        int lead = text.Length - text.TrimStart().Length;
        if (lead > 0) text = text.TrimStart();
        return (text.Trim(), string.Join(",", marks));
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicIsland-MediaBridge/" + UpdateChecker.BridgeVersion + " (https://github.com/gamerfun997-cloud/DynamicIsland-Dota)");
        return http;
    }

    public static async Task<string> GetAsync(string artist, string title, string album, double duration)
    {
        artist = artist.Trim();
        title = title.Trim();
        album = album.Trim();
        if (artist == "" || title == "") return "none";
        string key = artist + "\n" + title + "\n" + Math.Round(duration).ToString(CultureInfo.InvariantCulture);
        Task<string>? task;
        lock (Sync)
        {
            if (Cache.TryGetValue(key, out string? hit)) return hit;
            if (!Pending.TryGetValue(key, out task))
            {
                task = Task.Run(() => FetchAndStoreAsync(key, artist, title, album, duration));
                Pending[key] = task;
            }
        }
        var first = await Task.WhenAny(task, Task.Delay(1200));
        return first == task ? await task : "pending";
    }

    private static async Task<string> FetchAndStoreAsync(string key, string artist, string title, string album, double duration)
    {
        string res = await FetchAsync(artist, title, album, duration);
        lock (Sync)
        {
            Pending.Remove(key);
            if (!res.StartsWith("error", StringComparison.Ordinal))
            {
                if (Cache.Count > 300) Cache.Clear();
                Cache[key] = res;
            }
        }
        return res;
    }

    private static string E(string s) => Uri.EscapeDataString(s);

    private static async Task<string> FetchAsync(string artist, string title, string album, double duration)
    {
        try
        {
            string q = "track_name=" + E(title) + "&artist_name=" + E(artist);
            if (album != "") q += "&album_name=" + E(album);
            if (duration > 0) q += "&duration=" + Math.Round(duration).ToString(CultureInfo.InvariantCulture);
            string? found = null;
            using (var r = await Http.GetAsync(Api + "get?" + q))
            {
                if (r.IsSuccessStatusCode) found = Pick(await r.Content.ReadAsStringAsync(), duration);
            }
            found ??= await SearchAsync(artist, title, duration);
            string clean = NoiseRegex().Replace(title, "").Trim();
            if (found == null && clean != "" && clean != title) found = await SearchAsync(artist, clean, duration);
            return found ?? "none";
        }
        catch (Exception ex)
        {
            return "error " + ex.GetType().Name;
        }
    }

    private static async Task<string?> SearchAsync(string artist, string title, double duration)
    {
        using var r = await Http.GetAsync(Api + "search?track_name=" + E(title) + "&artist_name=" + E(artist));
        if (!r.IsSuccessStatusCode) return null;
        return Pick(await r.Content.ReadAsStringAsync(), duration);
    }

    private static string? Pick(string json, double duration)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object) return FromRecord(root, duration, false);
        if (root.ValueKind != JsonValueKind.Array) return null;
        string? best = null;
        double bestGap = double.MaxValue;
        bool instrumental = false;
        foreach (var rec in root.EnumerateArray())
        {
            string? res = FromRecord(rec, duration, true);
            if (res == null) continue;
            if (res == "instrumental")
            {
                instrumental = true;
                continue;
            }
            double gap = duration > 0 && rec.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? Math.Abs(d.GetDouble() - duration) : 0;
            if (gap < bestGap)
            {
                bestGap = gap;
                best = res;
            }
        }
        return best ?? (instrumental ? "instrumental" : null);
    }

    private static string? FromRecord(JsonElement rec, double duration, bool strictDuration)
    {
        if (strictDuration && duration > 0 && rec.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number && Math.Abs(d.GetDouble() - duration) > 4) return null;
        if (rec.TryGetProperty("instrumental", out var ins) && ins.ValueKind == JsonValueKind.True) return "instrumental";
        if (!rec.TryGetProperty("syncedLyrics", out var syn) || syn.ValueKind != JsonValueKind.String) return null;
        string lines = Parse(syn.GetString() ?? "");
        return lines == "" ? null : "ok\n" + lines;
    }

    private static string Parse(string lrc)
    {
        var list = new List<(long ms, string text, string words)>();
        foreach (string raw in lrc.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            var stamps = StampRegex().Matches(line);
            if (stamps.Count == 0) continue;
            int end = 0;
            var lead = new List<Match>();
            foreach (Match m in stamps)
            {
                if (m.Index != end) break;
                lead.Add(m);
                end = m.Index + m.Length;
            }
            if (lead.Count == 0) continue;
            var (text, words) = Words(line[end..].Replace('\t', ' '));
            foreach (Match m in lead) list.Add((Ms(m), text, words));
        }
        list.Sort((a, b) => a.ms.CompareTo(b.ms));
        var sb = new StringBuilder();
        foreach (var (ms, text, words) in list)
        {
            sb.Append(ms.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(text);
            if (words != "") sb.Append('\t').Append(words);
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
