using System.Text.Json;
using AIDotNet.Toon;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AIDotNet.Toon.ModelBenches;

internal enum BenchmarkFormat
{
    Toon,
    JsonPretty,
    JsonCompact,
    Yaml
}

internal static class Formatters
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };

    private static readonly ISerializer Yaml = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public static string Format(BenchmarkFormat fmt, object data)
    {
        return fmt switch
        {
            BenchmarkFormat.Toon => ToonSerializer.Serialize(data, new ToonSerializerOptions
            {
                Indent = 2,
                Delimiter = ToonDelimiter.COMMA,
                Strict = true,
                LengthMarker = null
            }),
            BenchmarkFormat.JsonPretty => JsonSerializer.Serialize(data, Pretty),
            BenchmarkFormat.JsonCompact => JsonSerializer.Serialize(data, Compact),
            BenchmarkFormat.Yaml => Yaml.Serialize(data),
            _ => throw new ArgumentOutOfRangeException(nameof(fmt), fmt, null)
        };
    }

    public static string CodeFenceLanguage(BenchmarkFormat fmt) => fmt switch
    {
        BenchmarkFormat.JsonPretty => "json",
        BenchmarkFormat.JsonCompact => "json",
        BenchmarkFormat.Yaml => "yaml",
        BenchmarkFormat.Toon => "toon",
        _ => ""
    };

    public static string DisplayName(BenchmarkFormat fmt) => fmt switch
    {
        BenchmarkFormat.Toon => "TOON",
        BenchmarkFormat.JsonPretty => "JSON",
        BenchmarkFormat.JsonCompact => "JSON compact",
        BenchmarkFormat.Yaml => "YAML",
        _ => fmt.ToString()
    };
}

internal sealed class BenchmarkTask
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string Instruction { get; init; } = string.Empty;
    public Func<object> BuildInput { get; init; } = () => new { };
    public Func<object, string> ExpectedAnswer { get; init; } = _ => string.Empty;

    public Func<string, object, bool> Scorer { get; init; } = (answer, input) =>
        string.Equals(answer.Trim(), "", StringComparison.OrdinalIgnoreCase);
}

internal static class TaskCatalog
{
    public static IReadOnlyList<BenchmarkTask> All()
    {
        var rng = new Random(1234);

        // Task 1: Multiplication
        var math = new BenchmarkTask
        {
            Id = "math",
            Name = "整数相乘",
            Instruction = "Given the input object, multiply the two integers a and b. Return only the integer result.",
            BuildInput = () => new
            {
                a = rng.Next(10, 50),
                b = rng.Next(10, 50),
                meta = new
                {
                    requestId = $"req-{rng.Next(100000, 999999)}",
                    tags = new[] { "math", "benchmark" }
                },
                audit = new
                {
                    createdAt = DateTime.UtcNow.ToString("o"),
                    source = rng.NextDouble() > 0.5 ? "web" : "api",
                    flags = new[] { "test", rng.NextDouble() > 0.5 ? "fast" : "slow" }
                }
            },
            ExpectedAnswer = input =>
            {
                var json = JsonSerializer.Serialize(input);
                using var doc = JsonDocument.Parse(json);
                int a = doc.RootElement.GetProperty("a").GetInt32();
                int b = doc.RootElement.GetProperty("b").GetInt32();
                return (a * b).ToString();
            },
            Scorer = (answer, input) =>
            {
                var s = new string(answer.Trim().Where(char.IsDigit).ToArray());
                if (string.IsNullOrEmpty(s)) return false;
                var expected = int.Parse(TaskCatalog.All().First(t => t.Id == "math").ExpectedAnswer(input));
                return int.TryParse(s, out var got) && got == expected;
            }
        };

        // Task 2: Email extraction
        var extract = new BenchmarkTask
        {
            Id = "extract-email",
            Name = "提取邮箱",
            Instruction = "From the customer object, return only the email field value.",
            BuildInput = () => new
            {
                customer = new
                {
                    id = rng.Next(1000, 9999),
                    name = $"User-{rng.Next(10000, 99999)}",
                    email = $"user{rng.Next(1000, 9999)}@example.com",
                    phone = $"+1-202-{rng.Next(100, 999)}-{rng.Next(1000, 9999)}",
                    addresses = new[]
                    {
                        new
                        {
                            type = "home", city = "Seattle", zip = $"{rng.Next(10000, 99999)}",
                            location = new
                            {
                                lat = Math.Round(rng.NextDouble() * 90, 4), lon = Math.Round(rng.NextDouble() * 180, 4)
                            },
                            lines = new[] { "Line1", "Line2" }
                        },
                        new
                        {
                            type = "work", city = "San Francisco", zip = $"{rng.Next(10000, 99999)}",
                            location = new
                            {
                                lat = Math.Round(rng.NextDouble() * 90, 4), lon = Math.Round(rng.NextDouble() * 180, 4)
                            },
                            lines = new[] { "Market St", "Suite 500" }
                        }
                    },
                    preferences = new { newsletter = rng.NextDouble() > 0.5, smsOptIn = rng.NextDouble() > 0.7 },
                    company = new
                    {
                        name = rng.NextDouble() > 0.5 ? "Contoso" : "Fabrikam",
                        contacts = new[]
                        {
                            new { name = "Alice", role = "manager", email = "alice@corp.example" },
                            new { name = "Bob", role = "assistant", email = "bob@corp.example" }
                        }
                    },
                    history = Enumerable.Range(0, rng.Next(1, 4)).Select(i => new
                    {
                        at = DateTime.UtcNow.AddDays(-i).ToString("o"),
                        action = i % 2 == 0 ? "login" : "purchase",
                        details = new { channel = i % 2 == 0 ? "web" : "mobile", ip = $"10.0.0.{rng.Next(1, 255)}" }
                    }).ToArray()
                },
                meta = new { source = "crm", requestId = $"req-{rng.Next(100000, 999999)}" }
            },
            ExpectedAnswer = input =>
            {
                var json = JsonSerializer.Serialize(input);
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("customer").GetProperty("email").GetString() ?? string.Empty;
            },
            Scorer = (answer, input) =>
            {
                var got = answer.Trim();
                var exp = TaskCatalog.All().First(t => t.Id == "extract-email").ExpectedAnswer(input).Trim();
                return string.Equals(got, exp, StringComparison.OrdinalIgnoreCase);
            }
        };

        // Task 3: Count items
        var count = new BenchmarkTask
        {
            Id = "count-items",
            Name = "统计项目数量",
            Instruction = "Given an array named items, return only the count of items as an integer.",
            BuildInput = () => new
            {
                items = Enumerable.Range(0, rng.Next(3, 9))
                    .Select(i => new
                    {
                        sku = $"SKU{i:000}",
                        qty = rng.Next(1, 5),
                        price = Math.Round(rng.NextDouble() * 20 + 5, 2),
                        supplier = new
                            { id = $"SUP-{rng.Next(100, 999)}", name = rng.NextDouble() > 0.5 ? "Acme" : "Globex" },
                        dimensions = new
                        {
                            w = Math.Round(rng.NextDouble() * 10 + 1, 2), h = Math.Round(rng.NextDouble() * 10 + 1, 2),
                            d = Math.Round(rng.NextDouble() * 10 + 1, 2)
                        },
                        tags = new[] { "fragile", i % 2 == 0 ? "promo" : "regular" }
                    })
                    .ToArray(),
                meta = new { warehouse = $"W{rng.Next(1, 4)}", batch = $"B{rng.Next(100, 999)}" },
                shipping = new { priority = rng.NextDouble() > 0.5, window = new { start = "09:00", end = "18:00" } }
            },
            ExpectedAnswer = input =>
            {
                var json = JsonSerializer.Serialize(input);
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("items").GetArrayLength().ToString();
            },
            Scorer = (answer, input) =>
            {
                var s = new string(answer.Trim().Where(char.IsDigit).ToArray());
                if (string.IsNullOrEmpty(s)) return false;
                var exp = int.Parse(TaskCatalog.All().First(t => t.Id == "count-items").ExpectedAnswer(input));
                return int.TryParse(s, out var got) && got == exp;
            }
        };

        // Task 4: Sentiment classification
        var sentiment = new BenchmarkTask
        {
            Id = "sentiment",
            Name = "情感分类",
            Instruction =
                "Given the input with a field 'text', classify sentiment as exactly one word: positive or negative. Return only that word.",
            BuildInput = () => new
            {
                text = rng.NextDouble() > 0.5
                    ? "I absolutely loved this product, it works flawlessly and made my day!"
                    : "This was terrible and frustrating; I would not recommend it to anyone.",
                context = new { lang = "en", channel = "web" },
                user = new
                {
                    id = $"U{rng.Next(1000, 9999)}",
                    profile = new { age = rng.Next(18, 70), region = rng.NextDouble() > 0.5 ? "NA" : "EU" }
                },
                thread = new
                {
                    id = $"T{rng.Next(10000, 99999)}",
                    messages = Enumerable.Range(0, rng.Next(2, 5)).Select(i => new
                    {
                        id = i + 1,
                        author = i % 2 == 0 ? "user" : "agent",
                        body = i % 2 == 0 ? "sample question" : "sample answer",
                        ts = DateTime.UtcNow.AddMinutes(-i).ToString("o")
                    }).ToArray()
                },
                attachments = new[]
                {
                    new { type = "image", url = "https://example.com/a.png" },
                    new { type = "link", url = "https://example.com" }
                },
                meta = new { requestId = $"req-{rng.Next(100000, 999999)}" }
            },
            ExpectedAnswer = input =>
            {
                var json = JsonSerializer.Serialize(input);
                using var doc = JsonDocument.Parse(json);
                var text = doc.RootElement.GetProperty("text").GetString() ?? "";
                return text.Contains("loved", StringComparison.OrdinalIgnoreCase) ||
                       text.Contains("flawlessly", StringComparison.OrdinalIgnoreCase)
                    ? "positive"
                    : "negative";
            },
            Scorer = (answer, input) =>
            {
                var got = answer.Trim().ToLowerInvariant();
                got = got.Replace(".", "");
                var exp = TaskCatalog.All().First(t => t.Id == "sentiment").ExpectedAnswer(input).Trim()
                    .ToLowerInvariant();
                return got == exp;
            }
        };

        // Task 5: Total revenue
        var revenue = new BenchmarkTask
        {
            Id = "total-revenue",
            Name = "总收入",
            Instruction =
                "Given an array of orders with 'price' and 'quantity', compute total revenue as a number with two decimals. Return only the number.",
            BuildInput = () => new
            {
                currency = "USD",
                channel = rng.NextDouble() > 0.5 ? "online" : "retail",
                store = new { id = $"S{rng.Next(100, 999)}", region = rng.NextDouble() > 0.5 ? "APAC" : "EMEA" },
                orders = Enumerable.Range(0, rng.Next(2, 5))
                    .Select(i => new
                    {
                        id = i + 1,
                        sku = $"SKU-{rng.Next(1000, 9999)}",
                        price = Math.Round(rng.NextDouble() * 90 + 10, 2),
                        quantity = rng.Next(1, 5),
                        tax = Math.Round(rng.NextDouble() * 0.2, 2),
                        discount = rng.NextDouble() > 0.7 ? Math.Round(rng.NextDouble() * 5, 2) : 0,
                        shipping = new
                        {
                            method = rng.NextDouble() > 0.5 ? "air" : "ground",
                            cost = Math.Round(rng.NextDouble() * 10, 2)
                        },
                        customer = new
                            { id = $"C{rng.Next(1000, 9999)}", tier = rng.NextDouble() > 0.5 ? "gold" : "silver" }
                    })
                    .ToArray(),
                meta = new { batch = rng.Next(1, 1000) }
            },
            ExpectedAnswer = input =>
            {
                var json = JsonSerializer.Serialize(input);
                using var doc = JsonDocument.Parse(json);
                var sum = 0.0;
                foreach (var ord in doc.RootElement.GetProperty("orders").EnumerateArray())
                {
                    var price = ord.GetProperty("price").GetDouble();
                    var qty = ord.GetProperty("quantity").GetInt32();
                    sum += price * qty;
                }

                return Math.Round(sum, 2).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            },
            Scorer = (answer, input) =>
            {
                var s = answer.Trim();
                // normalize number
                if (!double.TryParse(s, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var got))
                {
                    // try to extract first number
                    var digits = new string(s.Where(ch => char.IsDigit(ch) || ch == '.' || ch == '-').ToArray());
                    if (!double.TryParse(digits, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out got))
                        return false;
                }

                var exp = double.Parse(TaskCatalog.All().First(t => t.Id == "total-revenue").ExpectedAnswer(input),
                    System.Globalization.CultureInfo.InvariantCulture);
                return Math.Abs(got - exp) < 0.01;
            }
        };

        // Task 6: Sort numbers
        var sortNumbers = new BenchmarkTask
        {
            Id = "sort-numbers",
            Name = "数字排序",
            Instruction =
                "Given an array named numbers, return the numbers in ascending order as a single comma-separated list with no spaces (e.g., 1,2,3). Return only that list.",
            BuildInput = () => new
            {
                numbers = Enumerable.Range(0, rng.Next(8, 13)).Select(_ => rng.Next(0, 100)).ToArray(),
                info = new { source = "generated", allowDuplicates = true },
                extra = new
                {
                    matrix = Enumerable.Range(0, 3)
                        .Select(_ => Enumerable.Range(0, 3).Select(__ => rng.Next(0, 10)).ToArray()).ToArray(),
                    notes = new[]
                    {
                        new { k = "hint", v = "ascending" },
                        new { k = "format", v = "csv" }
                    }
                }
            },
            ExpectedAnswer = input =>
            {
                var json = JsonSerializer.Serialize(input);
                using var doc = JsonDocument.Parse(json);
                var nums = doc.RootElement.GetProperty("numbers").EnumerateArray().Select(x => x.GetInt32())
                    .OrderBy(x => x).ToArray();
                return string.Join(',', nums);
            },
            Scorer = (answer, input) =>
            {
                var raw = answer.Trim();
                var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 0) return false;
                if (!parts.All(p => int.TryParse(p, out _))) return false;
                var got = parts.Select(int.Parse).ToArray();
                var expStr = TaskCatalog.All().First(t => t.Id == "sort-numbers").ExpectedAnswer(input);
                var exp = expStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(int.Parse).ToArray();
                return got.SequenceEqual(exp);
            }
        };

        // Task 7: Unique count
        var uniqueCount = new BenchmarkTask
        {
            Id = "unique-count",
            Name = "唯一值计数",
            Instruction =
                "Given an array named values containing strings, return only the count of unique values as an integer.",
            BuildInput = () => new
            {
                values = Enumerable.Range(0, rng.Next(10, 15))
                    .Select(_ => "v" + rng.Next(0, 6)) // induce duplicates
                    .ToArray(),
                tags = new[] { "alpha", "beta", "gamma" },
                aliasGroups = new[]
                {
                    new[] { "v0", "v00" },
                    new[] { "v1", "v01", "v001" }
                },
                meta = new { requestId = $"req-{rng.Next(100000, 999999)}" }
            },
            ExpectedAnswer = input =>
            {
                var json = JsonSerializer.Serialize(input);
                using var doc = JsonDocument.Parse(json);
                var set = new HashSet<string>(doc.RootElement.GetProperty("values").EnumerateArray()
                    .Select(x => x.GetString() ?? ""));
                return set.Count.ToString();
            },
            Scorer = (answer, input) =>
            {
                var s = new string(answer.Trim().Where(char.IsDigit).ToArray());
                if (string.IsNullOrEmpty(s)) return false;
                var exp = int.Parse(TaskCatalog.All().First(t => t.Id == "unique-count").ExpectedAnswer(input));
                return int.TryParse(s, out var got) && got == exp;
            }
        };

        // Task 8: Date difference
        var dateDiff = new BenchmarkTask
        {
            Id = "date-diff",
            Name = "日期差值",
            Instruction =
                "Given fields start and end as dates in yyyy-MM-dd, return only the number of days between end and start as an integer.",
            BuildInput = () =>
            {
                var baseDate = new DateTime(2025, 1, 1).AddDays(rng.Next(0, 60));
                var end = baseDate.AddDays(rng.Next(1, 40));
                return new
                {
                    start = baseDate.ToString("yyyy-MM-dd"),
                    end = end.ToString("yyyy-MM-dd"),
                    calendar = "gregorian",
                    tz = "UTC",
                    schedule = new
                    {
                        holidays = new[] { "2025-01-01", "2025-02-01" },
                        workdays = new[] { "Mon", "Tue", "Wed", "Thu", "Fri" }
                    },
                    meta = new { requestId = $"req-{rng.Next(100000, 999999)}" }
                };
            },
            ExpectedAnswer = input =>
            {
                var json = JsonSerializer.Serialize(input);
                using var doc = JsonDocument.Parse(json);
                var start = DateTime.Parse(doc.RootElement.GetProperty("start").GetString()!, null,
                    System.Globalization.DateTimeStyles.AssumeUniversal).Date;
                var end = DateTime.Parse(doc.RootElement.GetProperty("end").GetString()!, null,
                    System.Globalization.DateTimeStyles.AssumeUniversal).Date;
                return (end - start).Days.ToString();
            },
            Scorer = (answer, input) =>
            {
                var s = new string(answer.Trim().Where(char.IsDigit).ToArray());
                if (string.IsNullOrEmpty(s)) return false;
                var exp = int.Parse(TaskCatalog.All().First(t => t.Id == "date-diff").ExpectedAnswer(input));
                return int.TryParse(s, out var got) && got == exp;
            }
        };

        // Task 9: Sum qty where price > threshold
        var sumQty = new BenchmarkTask
        {
            Id = "sum-qty-price-gt",
            Name = "超过阈值数量求和",
            Instruction =
                "Given an array items with fields price and qty, and a field threshold, return only the sum of qty for items where price > threshold as an integer.",
            BuildInput = () =>
            {
                var threshold = Math.Round(20 + rng.NextDouble() * 60, 2);
                var items = Enumerable.Range(0, rng.Next(4, 8))
                    .Select(_ => new { price = Math.Round(rng.NextDouble() * 100, 2), qty = rng.Next(1, 6) })
                    .ToArray();
                return new
                {
                    threshold,
                    currency = "USD",
                    items = items.Select((it, idx) => new
                    {
                        it.price,
                        it.qty,
                        name = $"Item-{idx + 1}",
                        extra = new
                        {
                            category = rng.NextDouble() > 0.5 ? "A" : "B",
                            attrs = new
                            {
                                weight = Math.Round(rng.NextDouble() * 5, 2), fragile = rng.NextDouble() > 0.6
                            }
                        }
                    }).ToArray(),
                    meta = new { batch = rng.Next(100, 999) }
                };
            },
            ExpectedAnswer = input =>
            {
                var json = JsonSerializer.Serialize(input);
                using var doc = JsonDocument.Parse(json);
                var threshold = doc.RootElement.GetProperty("threshold").GetDouble();
                var sum = 0;
                foreach (var it in doc.RootElement.GetProperty("items").EnumerateArray())
                {
                    var price = it.GetProperty("price").GetDouble();
                    var qty = it.GetProperty("qty").GetInt32();
                    if (price > threshold) sum += qty;
                }

                return sum.ToString();
            },
            Scorer = (answer, input) =>
            {
                var s = new string(answer.Trim().Where(char.IsDigit).ToArray());
                if (string.IsNullOrEmpty(s)) return false;
                var exp = int.Parse(TaskCatalog.All().First(t => t.Id == "sum-qty-price-gt").ExpectedAnswer(input));
                return int.TryParse(s, out var got) && got == exp;
            }
        };

        // Task 10: Max score -> name
        var maxScoreName = new BenchmarkTask
        {
            Id = "max-score-name",
            Name = "最高分名称",
            Instruction =
                "Given an array entries with fields name (string) and score (number), return only the name of the entry with the highest score.",
            BuildInput = () => new
            {
                entries = Enumerable.Range(0, rng.Next(3, 7))
                    .Select(i => new
                    {
                        id = i + 1,
                        name = $"N{i}",
                        score = Math.Round(rng.NextDouble() * 100, 3),
                        info = new { group = $"G{rng.Next(1, 4)}", weight = Math.Round(rng.NextDouble(), 2) }
                    })
                    .ToArray(),
                meta = new { group = $"G{rng.Next(1, 5)}" }
            },
            ExpectedAnswer = input =>
            {
                var json = JsonSerializer.Serialize(input);
                using var doc = JsonDocument.Parse(json);
                string name = "";
                double best = double.MinValue;
                foreach (var e in doc.RootElement.GetProperty("entries").EnumerateArray())
                {
                    var sc = e.GetProperty("score").GetDouble();
                    if (sc > best)
                    {
                        best = sc;
                        name = e.GetProperty("name").GetString() ?? "";
                    }
                }

                return name;
            },
            Scorer = (answer, input) =>
            {
                var got = (answer ?? string.Empty).Trim();
                var exp = TaskCatalog.All().First(t => t.Id == "max-score-name").ExpectedAnswer(input).Trim();
                return string.Equals(got, exp, StringComparison.OrdinalIgnoreCase);
            }
        };

        // Task 11: Nested primary postalCode selection
        var primaryPostal = new BenchmarkTask
        {
            Id = "primary-postal",
            Name = "主地址邮编",
            Instruction =
                "From the profile.addresses array, return only the postalCode of the address where primary is true.",
            BuildInput = () =>
            {
                var count = rng.Next(2, 5);
                var primaryIndex = rng.Next(0, count);
                var addresses = Enumerable.Range(0, count)
                    .Select(i => new
                    {
                        postalCode = $"{rng.Next(10000, 99999)}",
                        primary = i == primaryIndex,
                        city = $"City{rng.Next(1, 50)}",
                        country = "US",
                        location = new
                            { lat = Math.Round(rng.NextDouble() * 90, 4), lon = Math.Round(rng.NextDouble() * 180, 4) },
                        lines = new[] { "Addr Line 1", "Addr Line 2" }
                    })
                    .ToArray();
                return new
                {
                    profile = new
                    {
                        name = $"User-{rng.Next(1000, 9999)}",
                        contacts = new[]
                        {
                            new { type = "email", value = "u@example.com" },
                            new { type = "phone", value = "+1-555-0100" }
                        },
                        addresses
                    }
                };
            },
            ExpectedAnswer = input =>
            {
                var json = JsonSerializer.Serialize(input);
                using var doc = JsonDocument.Parse(json);
                foreach (var a in doc.RootElement.GetProperty("profile").GetProperty("addresses").EnumerateArray())
                {
                    if (a.GetProperty("primary").GetBoolean())
                        return a.GetProperty("postalCode").GetString() ?? string.Empty;
                }

                return string.Empty;
            },
            Scorer = (answer, input) =>
            {
                var got = (answer ?? string.Empty).Trim();
                var exp = TaskCatalog.All().First(t => t.Id == "primary-postal").ExpectedAnswer(input).Trim();
                return string.Equals(got, exp, StringComparison.Ordinal);
            }
        };

        // Task 12: Boolean logic
        var boolLogic = new BenchmarkTask
        {
            Id = "bool-logic",
            Name = "布尔逻辑",
            Instruction =
                "Given boolean fields a, b, c, evaluate (a AND NOT b) OR c and return only 'true' or 'false'.",
            BuildInput = () => new
            {
                a = rng.NextDouble() > 0.5,
                b = rng.NextDouble() > 0.5,
                c = rng.NextDouble() > 0.5,
                rule = "(a AND NOT b) OR c",
                context = new
                {
                    evaluationId = $"E{rng.Next(10000, 99999)}",
                    history = Enumerable.Range(0, 3).Select(i => new
                    {
                        a = i % 2 == 0,
                        b = i % 3 == 0,
                        c = i % 4 == 0,
                        ts = DateTime.UtcNow.AddSeconds(-i * 10).ToString("o")
                    }).ToArray()
                },
                meta = new { requestId = $"req-{rng.Next(100000, 999999)}" }
            },
            ExpectedAnswer = input =>
            {
                var json = JsonSerializer.Serialize(input);
                using var doc = JsonDocument.Parse(json);
                var a = doc.RootElement.GetProperty("a").GetBoolean();
                var b = doc.RootElement.GetProperty("b").GetBoolean();
                var c = doc.RootElement.GetProperty("c").GetBoolean();
                var res = (a && !b) || c;
                return res ? "true" : "false";
            },
            Scorer = (answer, input) =>
            {
                var got = (answer ?? string.Empty).Trim().ToLowerInvariant();
                if (got.EndsWith(".")) got = got.TrimEnd('.');
                var exp = TaskCatalog.All().First(t => t.Id == "bool-logic").ExpectedAnswer(input).Trim()
                    .ToLowerInvariant();
                return got == exp;
            }
        };

        return new[]
        {
            math, extract, count, sentiment, revenue,
            sortNumbers, uniqueCount, dateDiff, sumQty, maxScoreName, primaryPostal, boolLogic
        };
    }
}