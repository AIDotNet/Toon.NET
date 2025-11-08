using System.Text.Json;
using System.Text.RegularExpressions;
using AIDotNet.Toon;
using SharpToken;

namespace AIDotNet.Toon.Benchmarks;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonPretty = new()
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions JsonCompact = new()
    {
        WriteIndented = false
    };

    // Simple display names similar to TS benchmark
    private static readonly Dictionary<string, string> FormatterDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["json-pretty"] = "JSON",
        ["json-compact"] = "JSON compact",
        ["toon"] = "TOON",
    };

    private static readonly Dictionary<string, Func<object, string>> Formatters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["json-pretty"] = data => JsonSerializer.Serialize(data, JsonPretty),
        ["json-compact"] = data => JsonSerializer.Serialize(data, JsonCompact),
        ["toon"] = data => ToonSerializer.Serialize(data, new ToonSerializerOptions
        {
            Indent = 2,
            Delimiter = ToonDelimiter.COMMA,
            Strict = true,
            LengthMarker = null
        })
    };

    // Cache tokenizer instance. Prefer o200k_base; fallback to cl100k_base to approximate TS benchmarks.
    private static object? sTokenEncoding;

    private sealed record BenchmarkExample(
        string Name,
        string Emoji,
        string Description,
        Func<object> GetData,
        bool ShowDetailed
    );

    private sealed record FormatMetrics(string Name, int Tokens, int Savings, string SavingsPercent);

    private sealed record BenchmarkResult(
        string Name,
        string Emoji,
        string Description,
        object Data,
        List<FormatMetrics> Formats,
        bool ShowDetailed
    );

    public static void Main()
    {
        Console.WriteLine("Token Efficiency Benchmark (.NET)");

        var examples = BuildExamples();
        var results = new List<BenchmarkResult>();
        var totalTokensByFormat = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var example in examples)
        {
            var data = example.GetData();

            // per-format tokens
            var formatMetrics = new List<FormatMetrics>();
            var tokensByFormat = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in Formatters)
            {
                var formatted = kv.Value(data);
                var tokens = Tokenize(formatted);
                tokensByFormat[kv.Key] = tokens;
                totalTokensByFormat[kv.Key] = (totalTokensByFormat.TryGetValue(kv.Key, out var acc) ? acc : 0) + tokens;
            }

            var toonTokens = tokensByFormat["toon"];
            foreach (var kv in tokensByFormat)
            {
                var savings = kv.Value - toonTokens;
                var savingsPercent = kv.Key.Equals("toon", StringComparison.OrdinalIgnoreCase)
                    ? "0.0"
                    : ((savings / (double)kv.Value) * 100.0).ToString("0.0");

                formatMetrics.Add(new FormatMetrics(
                    kv.Key,
                    kv.Value,
                    savings,
                    savingsPercent
                ));
            }

            results.Add(new BenchmarkResult(
                example.Name,
                example.Emoji,
                example.Description,
                data,
                formatMetrics,
                example.ShowDetailed
            ));
        }

        // Totals row
        var totalToonTokens = (double)totalTokensByFormat["toon"];
        var totalSavingsPercent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in totalTokensByFormat)
        {
            if (kv.Key.Equals("toon", StringComparison.OrdinalIgnoreCase))
            {
                totalSavingsPercent[kv.Key] = "0.0";
            }
            else
            {
                var savings = kv.Value - totalToonTokens;
                totalSavingsPercent[kv.Key] = ((savings / kv.Value) * 100.0).ToString("0.0");
            }
        }

        // Build ASCII bar section (similar compact layout to TS)
        // We use json-pretty as the baseline for percentage bar, mirroring TS style
        var formatOrder = new[] { "json-pretty", "json-compact" };
        var datasetRows = new List<string>();

        foreach (var result in results)
        {
            var toon = result.Formats.First(f => f.Name.Equals("toon", StringComparison.OrdinalIgnoreCase));
            var jsonPretty = result.Formats.First(f => f.Name.Equals("json-pretty", StringComparison.OrdinalIgnoreCase));

            var percentage = ParsePercent(jsonPretty.SavingsPercent);
            var bar = CreateProgressBar(100 - percentage, 100); // invert to show TOON tokens
            var toonStr = toon.Tokens.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

            var line1 = $"{result.Emoji} {result.Name.PadRight(25)} {bar}   {toonStr.PadLeft(6)} tokens";

            var comparisonLines = new List<string>();
            foreach (var fname in formatOrder)
            {
                var fmt = result.Formats.First(f => f.Name.Equals(fname, StringComparison.OrdinalIgnoreCase));
                var label = FormatterDisplayNames.TryGetValue(fname, out var disp) ? disp : fname.ToUpperInvariant();
                var labelWithSavings = $"vs {label} (-{fmt.SavingsPercent}%)".PadRight(27);
                var tokenStr = fmt.Tokens.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).PadLeft(6);
                comparisonLines.Add($"                             {labelWithSavings}{tokenStr}");
            }

            datasetRows.Add(string.Join(Environment.NewLine, new[] { line1 }.Concat(comparisonLines)));
        }

        var datasetSection = string.Join(Environment.NewLine + Environment.NewLine, datasetRows);

        // Totals row and bar (TOON vs average of comparison formats)
        var comparisonTokens = formatOrder.Select(name => (double)totalTokensByFormat[name]).ToArray();
        var avgComparisonTokens = comparisonTokens.Average();
        var totalPercentage = (totalToonTokens / avgComparisonTokens) * 100.0;
        var totalBar = CreateProgressBar(totalPercentage, 100);

        var totalLine1 =
            $"Total                        {totalBar}   {totalToonTokens.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).PadLeft(6)} tokens";

        var totalsComparisons = new List<string>();
        foreach (var fname in formatOrder)
        {
            var label = FormatterDisplayNames.TryGetValue(fname, out var disp) ? disp : fname.ToUpperInvariant();
            var tokens = totalTokensByFormat[fname];
            var percent = totalSavingsPercent[fname];
            var labelWithSavings = $"vs {label} (-{percent}%)".PadRight(27);
            var tokenStr = tokens.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).PadLeft(6);
            totalsComparisons.Add($"                             {labelWithSavings}{tokenStr}");
        }

        var separator = "─────────────────────────────────────────────────────────────────────";
        var barChartSection = $"{datasetSection}{Environment.NewLine}{Environment.NewLine}{separator}{Environment.NewLine}{totalLine1}{Environment.NewLine}{string.Join(Environment.NewLine, totalsComparisons)}";

        // Detailed examples (GitHub and Analytics), truncated for display
        var detailedExamples = new List<string>();
        var filtered = results.Where(r => r.ShowDetailed).ToArray();
        for (int i = 0; i < filtered.Length; i++)
        {
            var r = filtered[i];
            object displayData = r.Data;

            if (r.Name.Equals("GitHub Repositories", StringComparison.OrdinalIgnoreCase))
            {
                // Keep 3 items, truncate description
                var repos = GetProperty(r.Data, "repositories") as IEnumerable<object>;
                if (repos != null)
                {
                    var sliced = repos.Take(3).Select(TrimRepoDescription).ToArray();
                    displayData = new Dictionary<string, object?> { ["repositories"] = sliced };
                }
            }
            else if (r.Name.Equals("Daily Analytics", StringComparison.OrdinalIgnoreCase))
            {
                var metrics = GetProperty(r.Data, "metrics") as IEnumerable<object>;
                if (metrics != null)
                {
                    displayData = new Dictionary<string, object?> { ["metrics"] = metrics.Take(5).ToArray() };
                }
            }

            var json = r.Formats.First(f => f.Name.Equals("json-pretty", StringComparison.OrdinalIgnoreCase));
            var toon = r.Formats.First(f => f.Name.Equals("toon", StringComparison.OrdinalIgnoreCase));

            var tailSep = i < filtered.Length - 1 ? Environment.NewLine + Environment.NewLine + "---" : string.Empty;

            var block = $$"""
#### {{r.Emoji}} {{r.Name}}

**Configuration:** {{r.Description}}

**Savings:** {{json.Savings.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}} tokens ({{json.SavingsPercent}}% reduction vs JSON)

**JSON** ({{json.Tokens.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}} tokens):

```json
{{JsonSerializer.Serialize(displayData, JsonPretty)}}
```

**TOON** ({{toon.Tokens.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}} tokens):

```
{{ToonSerializer.Serialize(displayData)}}
```{{tailSep}}
""";

            detailedExamples.Add(block);
        }

        var markdown = $$"""
### Token 效率

```
{{barChartSection}}
```

<details>
<summary><strong>查看详细示例</strong></summary>

{{string.Join(Environment.NewLine + Environment.NewLine, detailedExamples)}}

</details>
""".TrimStart();

        Console.WriteLine(barChartSection);
        var resultsDir = EnsureResultsDir();
        var outFile = Path.Combine(resultsDir, "token-efficiency.md");
        File.WriteAllText(outFile, markdown);
        Console.WriteLine($"Result saved to `{Path.GetRelativePath(GetRepoRoot(), outFile).Replace('\\', '/')}`");

        // Generate interactive HTML charts (Plotly) for clearer visualization
        var htmlFile = Path.Combine(resultsDir, "token-efficiency.html");
        GenerateHtmlReport(results, totalTokensByFormat, htmlFile);
        Console.WriteLine($"Interactive charts saved to `{Path.GetRelativePath(GetRepoRoot(), htmlFile).Replace('\\', '/')}`");
    }

    private static object TrimRepoDescription(object repo)
    {
        // best-effort trimming using reflection/dictionary
        var dict = ObjectToDictionary(repo);
        if (dict.TryGetValue("description", out var descObj) && descObj is string desc)
        {
            if (desc.Length > 80)
                dict["description"] = desc.Substring(0, 80) + "…";
        }
        return dict;
    }

    private static IDictionary<string, object?> ObjectToDictionary(object obj)
    {
        if (obj is IDictionary<string, object?> d)
            return new Dictionary<string, object?>(d, StringComparer.Ordinal);

        // Try System.Text.Json round-trip for anonymous/POCO objects
        var json = JsonSerializer.Serialize(obj);
        var doc = JsonDocument.Parse(json);
        return JsonElementToDictionary(doc.RootElement);

    }

    private static IDictionary<string, object?> JsonElementToDictionary(JsonElement el)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in el.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.Object => JsonElementToDictionary(prop.Value),
                JsonValueKind.Array => prop.Value.EnumerateArray().Select(FromJsonElement).ToArray(),
                _ => FromJsonElement(prop.Value)
            };
        }
        return dict;
    }

    private static object? FromJsonElement(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => JsonElementToDictionary(el),
            JsonValueKind.Array => el.EnumerateArray().Select(FromJsonElement).ToArray(),
            _ => null
        };
    }

    private static object? GetProperty(object obj, string name)
    {
        if (obj is IDictionary<string, object?> d)
        {
            d.TryGetValue(name, out var v);
            return v;
        }

        var t = obj.GetType();
        var pi = t.GetProperty(name);
        if (pi != null)
            return pi.GetValue(obj);

        // fallback via json
        var json = JsonSerializer.Serialize(obj);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty(name, out var value))
            return FromJsonElement(value);

        return null;
    }

    private static IEnumerable<BenchmarkExample> BuildExamples()
    {
        return new[]
        {
            new BenchmarkExample(
                Name: "GitHub Repositories",
                Emoji: "⭐",
                Description: "Top 100 GitHub repositories with stars, forks, and metadata",
                GetData: () => new Dictionary<string, object?>
                {
                    ["repositories"] = LoadGithubRepos()
                },
                ShowDetailed: true
            ),
            new BenchmarkExample(
                Name: "Daily Analytics",
                Emoji: "📈",
                Description: "180 days of web metrics (views, clicks, conversions, revenue)",
                GetData: () => new Dictionary<string, object?> { ["metrics"] = GenerateAnalytics(180) },
                ShowDetailed: true
            ),
            new BenchmarkExample(
                Name: "E-Commerce Order",
                Emoji: "🛒",
                Description: "Single nested order with customer and items",
                GetData: GenerateOrder,
                ShowDetailed: false
            ),
        };
    }

    private static string CreateProgressBar(double value, double max, int width = 28)
    {
        value = Math.Clamp(value, 0, max);
        var ratio = max > 0 ? value / max : 0;
        var filled = (int)Math.Round(ratio * width);
        var bar = new string('█', Math.Clamp(filled, 0, width)) + new string('░', Math.Clamp(width - filled, 0, width));
        return bar;
    }

    // Tokenizer using SharpToken to approximate tiktoken (TS uses gpt-tokenizer with o200k_base).
    // Attempts o200k_base first; if unavailable, falls back to cl100k_base.
    // Falls back to regex counting only if encoding unexpectedly fails.
    private static int Tokenize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        try
        {
            if (sTokenEncoding is null)
            {
                try
                {
                    sTokenEncoding = GptEncoding.GetEncoding("o200k_base");
                }
                catch
                {
                    sTokenEncoding = GptEncoding.GetEncoding("cl100k_base");
                }
            }

            dynamic enc = sTokenEncoding!;
            var tokens = enc.Encode(text);
            return (int)tokens.Count;
        }
        catch
        {
            // Final safety fallback: regex-based token approximation
            var rx = new Regex(@"[A-Za-z_]+|\d+|[^\sA-Za-z0-9_]", RegexOptions.Compiled);
            return rx.Matches(text).Count;
        }
    }

    private static double ParsePercent(string s)
    {
        if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
            return v;
        return 0;
    }

    // ========== Datasets (best-effort parity with TS) ==========

    private static IReadOnlyList<object> LoadGithubRepos()
    {
        // Try to read TS dataset: benchmarks/data/github-repos.json
        var root = GetRepoRoot();
        var p = Path.Combine(root, "benchmarks", "data", "github-repos.json");
        if (!File.Exists(p))
        {
            Console.WriteLine($"[warn] Cannot find GitHub dataset at: {p}. Using empty list.");
            return Array.Empty<object>();
        }

        using var fs = File.OpenRead(p);
        using var doc = JsonDocument.Parse(fs);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<object>();

        var list = new List<object>(doc.RootElement.GetArrayLength());
        foreach (var repo in doc.RootElement.EnumerateArray())
        {
            // Keep fields used in TS report examples
            var obj = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = TryNum(repo, "id"),
                ["name"] = TryStr(repo, "name"),
                ["repo"] = TryStr(repo, "repo") ?? JoinOwnerRepo(repo),
                ["description"] = TryStr(repo, "description"),
                ["createdAt"] = TryStr(repo, "createdAt"),
                ["updatedAt"] = TryStr(repo, "updatedAt"),
                ["pushedAt"] = TryStr(repo, "pushedAt"),
                ["stars"] = TryNum(repo, "stars"),
                ["watchers"] = TryNum(repo, "watchers"),
                ["forks"] = TryNum(repo, "forks"),
                ["defaultBranch"] = TryStr(repo, "defaultBranch"),
            };
            list.Add(obj);
        }
        return list;
    }

    private static object? TryNum(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var v))
        {
            if (v.ValueKind == JsonValueKind.Number)
                return v.TryGetInt64(out var i) ? i : v.GetDouble();
        }
        return null;
    }

    private static string? TryStr(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString();
        return null;
    }

    private static string? JoinOwnerRepo(JsonElement el)
    {
        var owner = TryStr(el, "owner");
        var name = TryStr(el, "name");
        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(name))
            return $"{owner}/{name}";
        return null;
    }

    private static IReadOnlyList<object> GenerateAnalytics(int days, string startDate = "2025-01-01")
    {
        var list = new List<object>(days);
        var date = DateTime.Parse(startDate, null, System.Globalization.DateTimeStyles.AssumeUniversal).Date;
        var rng = new Random(12345);

        for (int i = 0; i < days; i++)
        {
            var current = date.AddDays(i);
            var weekend = (current.DayOfWeek == DayOfWeek.Saturday || current.DayOfWeek == DayOfWeek.Sunday) ? 0.7 : 1.0;

            var baseViews = 5000;
            var views = (int)Math.Round(baseViews * weekend + NextRange(rng, -1000, 3000));
            var clicks = (int)Math.Round(views * NextFloat(rng, 0.02, 0.08));
            var conversions = (int)Math.Round(clicks * NextFloat(rng, 0.05, 0.15));
            var avgOrder = NextFloat(rng, 49.99, 299.99);
            var revenue = Math.Round(conversions * avgOrder, 2);
            var bounce = Math.Round(NextFloat(rng, 0.3, 0.7), 2);

            list.Add(new Dictionary<string, object?>
            {
                ["date"] = current.ToString("yyyy-MM-dd"),
                ["views"] = views,
                ["clicks"] = clicks,
                ["conversions"] = conversions,
                ["revenue"] = revenue,
                ["bounceRate"] = bounce
            });
        }

        return list;
    }

    private static object GenerateOrder()
    {
        var rng = new Random(67890);

        int ItemsCount() => rng.Next(2, 6);
        string RandSku(int len) => string.Concat(Enumerable.Range(0, len).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[rng.Next(36)]));

        var items = Enumerable.Range(0, ItemsCount()).Select(_ => new Dictionary<string, object?>
        {
            ["sku"] = RandSku(8),
            ["name"] = $"Product-{RandSku(4)}",
            ["quantity"] = rng.Next(1, 6),
            ["price"] = Math.Round(10 + rng.NextDouble() * 190, 2)
        }).ToArray();

        var total = Math.Round(items.Sum(i => Convert.ToDouble(i["price"]) * Convert.ToInt32(i["quantity"])), 2);

        return new Dictionary<string, object?>
        {
            ["orderId"] = RandSku(12),
            ["customer"] = new Dictionary<string, object?>
            {
                ["id"] = rng.Next(1000, 9999),
                ["name"] = $"User {RandSku(5)}",
                ["email"] = $"user{rng.Next(1000,9999)}@example.com",
                ["phone"] = $"+1-202-{rng.Next(100,999)}-{rng.Next(1000,9999)}"
            },
            ["items"] = items,
            ["total"] = total,
            ["status"] = new[] { "pending", "processing", "shipped", "delivered" }[rng.Next(4)],
            ["createdAt"] = DateTime.UtcNow.AddDays(-rng.Next(1, 7)).ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
    }

    private static double NextRange(Random rng, int min, int max) => rng.Next(min, max + 1);
    private static double NextFloat(Random rng, double min, double max) => min + rng.NextDouble() * (max - min);

    // ========== Paths / IO ==========

    private static string EnsureResultsDir()
    {
        var root = GetRepoRoot();
        var dir = Path.Combine(root, "benchmarks", "AIDotNet.Toon.Benchmarks", "results");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string GetRepoRoot()
    {
        // Walk up from BaseDirectory to find the solution marker
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var marker = Path.Combine(dir, "AIDotNet.Toon.sln");
            if (File.Exists(marker))
                return dir;

            var parent = Directory.GetParent(dir)?.FullName;
            if (string.Equals(parent, dir, StringComparison.OrdinalIgnoreCase))
                break;
            dir = parent ?? string.Empty;
        }

        // fallback to current directory
        return Directory.GetCurrentDirectory();
    }

    // ========== HTML charts (Plotly.js via CDN) ==========

    private static void GenerateHtmlReport(
        IReadOnlyList<BenchmarkResult> results,
        IDictionary<string, long> totalTokensByFormat,
        string htmlPath)
    {
        // Per-dataset arrays
        var datasetNames = results.Select(r => r.Name).ToArray();

        int[] TokensOf(string fmt) => results
            .Select(r => r.Formats.First(f => f.Name.Equals(fmt, StringComparison.OrdinalIgnoreCase)).Tokens)
            .ToArray();

        var toonTokens = TokensOf("toon");
        var jsonPrettyTokens = TokensOf("json-pretty");
        var jsonCompactTokens = TokensOf("json-compact");

        // Percent savings vs JSON (per dataset)
        double[] PctSavings(int[] baseJson, int[] other) => baseJson
            .Zip(other, (b, o) => b == 0 ? 0.0 : ((b - (double)o) / b) * 100.0)
            .ToArray();

        var pctToonVsJson = PctSavings(jsonPrettyTokens, toonTokens);
        var pctJsonCompactVsJson = PctSavings(jsonPrettyTokens, jsonCompactTokens);

        // Totals per format
        static long GetOrZero(IDictionary<string, long> map, string k)
            => map.TryGetValue(k, out var v) ? v : 0;

        var totalLabels = new[] { "TOON", "JSON", "JSON compact" };
        var totalValues = new[]
        {
            GetOrZero(totalTokensByFormat, "toon"),
            GetOrZero(totalTokensByFormat, "json-pretty"),
            GetOrZero(totalTokensByFormat, "json-compact"),
        };

        // JSON helpers for embedding
        static string J(object o) => JsonSerializer.Serialize(o);

        var html = $@"
<!DOCTYPE html>
<html lang=""zh"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>TOON Token Efficiency Charts</title>
  <script src=""https://cdn.plot.ly/plotly-2.27.0.min.js""></script>
  <style>
    body {{ font-family: system-ui, -apple-system, Segoe UI, Roboto, 'Helvetica Neue', Arial, 'Noto Sans', 'Liberation Sans', sans-serif; margin: 16px; }}
    .chart {{ width: 100%; max-width: 1100px; height: 480px; margin: 24px auto; }}
    h2 {{ margin: 24px 0 8px; }}
    .hint {{ color: #666; font-size: 0.95em; }}
  </style>
</head>
<body>
  <h1>Token 效率图表</h1>
  <div class=""hint"">由 .NET 基准（SharpToken o200k_base）生成的交互式图表。可悬停查看数据，点击图例可切换系列显示。</div>

  <h2>Tokens by Dataset and Format</h2>
  <div id=""chartTokensByDataset"" class=""chart""></div>

  <h2>Percent Savings vs JSON (per Dataset)</h2>
  <div id=""chartSavingsByDataset"" class=""chart""></div>

  <h2>Total Tokens by Format</h2>
  <div id=""chartTotals"" class=""chart""></div>

  <script>
    const datasetNames = {J(datasetNames)};
    const toon = {J(toonTokens)};
    const jsonPretty = {J(jsonPrettyTokens)};
    const jsonCompact = {J(jsonCompactTokens)};

    const pctToonVsJson = {J(pctToonVsJson)};
    const pctJsonCompactVsJson = {J(pctJsonCompactVsJson)};

    const totalLabels = {J(totalLabels)};
    const totalValues = {J(totalValues)};

    // Chart 1: Tokens by dataset (grouped bars)
    Plotly.newPlot('chartTokensByDataset', [
      {{ x: datasetNames, y: jsonPretty, type: 'bar', name: 'JSON', marker: {{ color: '#8884d8' }} }},
      {{ x: datasetNames, y: jsonCompact, type: 'bar', name: 'JSON 压缩', marker: {{ color: '#82ca9d' }} }},
      {{ x: datasetNames, y: toon, type: 'bar', name: 'TOON', marker: {{ color: '#ff7f50' }} }}
    ], {{
      barmode: 'group',
      yaxis: {{ title: 'Token 数' }},
      margin: {{ t: 20, r: 10, l: 60, b: 80 }},
      legend: {{ orientation: 'h', x: 0, y: 1.1 }}
    }}, {{ responsive: true }});

    // Chart 2: Percent savings vs JSON (per dataset)
    Plotly.newPlot('chartSavingsByDataset', [
      {{ x: datasetNames, y: pctToonVsJson, type: 'bar', name: 'TOON 相对 JSON', marker: {{ color: '#ff7f50' }} }},
      {{ x: datasetNames, y: pctJsonCompactVsJson, type: 'bar', name: 'JSON 压缩相对 JSON', marker: {{ color: '#82ca9d' }} }}
    ], {{
      barmode: 'group',
      yaxis: {{ title: '节省（%）' }},
      margin: {{ t: 20, r: 10, l: 60, b: 80 }},
      legend: {{ orientation: 'h', x: 0, y: 1.1 }}
    }}, {{ responsive: true }});

    // Chart 3: Totals by format
    Plotly.newPlot('chartTotals', [
      {{ x: totalLabels, y: totalValues, type: 'bar', name: '总 Token 数', marker: {{ color: ['#ff7f50','#8884d8','#82ca9d'] }} }}
    ], {{
      yaxis: {{ title: 'Token 数' }},
      margin: {{ t: 20, r: 10, l: 60, b: 60 }},
      showlegend: false
    }}, {{ responsive: true }});
  </script>
</body>
</html>".Trim();

        Directory.CreateDirectory(Path.GetDirectoryName(htmlPath)!);
        File.WriteAllText(htmlPath, html);
    }
}