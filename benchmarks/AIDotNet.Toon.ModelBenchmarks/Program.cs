using System.Collections.Concurrent;
using System.Text;
using Spectre.Console;

namespace AIDotNet.Toon.ModelBenches;

internal static class Program
{
    private static readonly global::AIDotNet.Toon.ModelBenches.BenchmarkFormat[] Formats = new[]
    {
        global::AIDotNet.Toon.ModelBenches.BenchmarkFormat.Toon,
        global::AIDotNet.Toon.ModelBenches.BenchmarkFormat.JsonPretty,
        global::AIDotNet.Toon.ModelBenches.BenchmarkFormat.Yaml,
        global::AIDotNet.Toon.ModelBenches.BenchmarkFormat.JsonCompact
    };

    public static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
    Console.WriteLine("模型格式准确性基准（.NET）");

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            Console.WriteLine("[错误] 缺少 OPENAI_API_KEY 环境变量，请先设置后再运行。");
            return;
        }

        var models = GetModels();
        var tasks = TaskCatalog.All();
        var runs = GetRuns();
        var totalStepsPerModel = tasks.Count * runs * Formats.Length;
        var allModelResults = new ConcurrentBag<ModelResults>();

        await AnsiConsole.Progress()
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new ElapsedTimeColumn(),
                new SpinnerColumn()
            })
            .StartAsync(async ctx =>
            {
                var modelThrottle = new SemaphoreSlim(initialCount: GetModelParallelism());
                var modelJobs = new List<Task>();

                foreach (var model in models)
                {
                    await modelThrottle.WaitAsync();
                    var progressTask = ctx.AddTask($"[blue]{model}[/]", autoStart: true, maxValue: totalStepsPerModel);
                    var job = Task.Run(async () =>
                    {
                        try
                        {
                            var modelResult = await RunModelAsync(model, tasks, runs, totalSteps: totalStepsPerModel, progressTask);
                            allModelResults.Add(modelResult);
                        }
                        finally
                        {
                            modelThrottle.Release();
                        }
                    });
                    modelJobs.Add(job);
                }

                await Task.WhenAll(modelJobs);
            });

        // Generate single unified report with all models
        var outDir = EnsureResultsDir();
        var reportPath = Path.Combine(outDir, "benchmark-report.html");
        ReportGenerator.GenerateUnifiedHtml(allModelResults.ToList(), reportPath);
        AnsiConsole.MarkupLine($"[green]综合报告已保存至[/] [link]{Path.GetRelativePath(GetRepoRoot(), reportPath).Replace('\\','/')}[/]");
    }

    private static async Task RunOneAsync(global::AIDotNet.Toon.ModelBenches.ModelClient client, global::AIDotNet.Toon.ModelBenches.BenchmarkTask t, object input, global::AIDotNet.Toon.ModelBenches.BenchmarkFormat fmt, ConcurrentBag<global::AIDotNet.Toon.ModelBenches.SingleResult> sink)
    {
        try
        {
            var formatted = Formatters.Format(fmt, input);
            var lang = Formatters.CodeFenceLanguage(fmt);

            var system = new ChatMessage("system", "You are a precise assistant. Follow the user instructions exactly and return only the requested answer.");
            var user = new ChatMessage("user",
                $"Instruction: {t.Instruction}\n\nFormat: {Formatters.DisplayName(fmt)}\n\nInput:\n```{lang}\n{formatted}\n```\n\nReturn only the final answer with no explanation.");

            var resp = await client.ChatAsync(new[] { system, user });

            var correct = t.Scorer(resp.Text, input);
            sink.Add(new SingleResult
            {
                TaskId = t.Id,
                TaskName = t.Name,
                FormatDisplay = Formatters.DisplayName(fmt),
                Correct = correct,
                PromptTokens = resp.PromptTokens,
                CompletionTokens = resp.CompletionTokens,
                TotalTokens = resp.TotalTokens,
                Answer = resp.Text
            });

            // Avoid noisy per-request logging; Spectre progress shows completion
        }
        catch (Exception ex)
        {
            sink.Add(new SingleResult
            {
                TaskId = t.Id,
                TaskName = t.Name,
                FormatDisplay = Formatters.DisplayName(fmt),
                Correct = false,
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                Answer = $"[error] {ex.GetType().Name}: {ex.Message}"
            });
            // Keep console quiet here to preserve progress layout
        }
    }

    private static async Task<ModelResults> RunModelAsync(string model, IReadOnlyList<global::AIDotNet.Toon.ModelBenches.BenchmarkTask> tasks, int runs, int totalSteps, Spectre.Console.ProgressTask progressTask)
    {
        var results = new ConcurrentBag<global::AIDotNet.Toon.ModelBenches.SingleResult>();
        using var client = new ModelClient(model);
        var throttle = new SemaphoreSlim(initialCount: GetParallelism());
        var jobs = new List<Task>();

        int done = 0, ok = 0, fail = 0;
        var lastUpdateTime = DateTime.UtcNow;
        var updateLock = new object();

        foreach (var task in tasks)
        {
            for (int run = 0; run < runs; run++)
            {
                var input = task.BuildInput();
                foreach (var fmt in Formats)
                {
                    await throttle.WaitAsync();
                    jobs.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await RunOneAsync(client, task, input, fmt, results);
                        }
                        finally
                        {
                            throttle.Release();
                            var last = Interlocked.Increment(ref done);
                            
                            // 限制更新频率：每200ms更新一次，或者每完成10%任务更新一次
                            bool shouldUpdate = false;
                            lock (updateLock)
                            {
                                var now = DateTime.UtcNow;
                                var elapsed = (now - lastUpdateTime).TotalMilliseconds;
                                if (elapsed >= 200 || last % Math.Max(1, totalSteps / 10) == 0 || last == totalSteps)
                                {
                                    lastUpdateTime = now;
                                    shouldUpdate = true;
                                }
                            }

                            if (shouldUpdate)
                            {
                                // 批量计算统计信息
                                var snapOk = results.Count(r => r.Correct);
                                var snapFail = results.Count(r => !r.Correct && !r.Answer.StartsWith("[error]"));
                                var snapErr = results.Count(r => r.Answer.StartsWith("[error]"));
                                ok = snapOk; 
                                fail = snapFail + snapErr;
                                
                                progressTask.Value = last;
                                progressTask.Description = $"[blue]{model}[/]  ok:[green]{ok}[/]  fail:[red]{fail}[/]  done:{last}/{totalSteps}";
                            }
                        }
                    }));
                }
            }
        }

        await Task.WhenAll(jobs);

        var grouped = results.GroupBy(r => r.FormatDisplay).OrderBy(g => g.Key).ToArray();
        var summary = grouped.Select(g => new global::AIDotNet.Toon.ModelBenches.FormatSummary
        {
            Format = ParseFormat(g.Key),
            FormatDisplay = g.Key,
            Accuracy = g.Average(r => r.Correct ? 1.0 : 0.0),
            AvgPromptTokens = g.Average(r => r.PromptTokens),
            AvgCompletionTokens = g.Average(r => r.CompletionTokens)
        }).ToList();

        // Print summary table for the model after completion
        var table = new Table().Title($"[yellow]{model} 总结[/]").AddColumns("格式", "准确率 %", "提示 Tokens", "生成 Tokens");
        foreach (var s in summary.OrderBy(s => s.FormatDisplay))
        {
            table.AddRow(s.FormatDisplay, (s.Accuracy * 100).ToString("0.0"), s.AvgPromptTokens.ToString("0.0"), s.AvgCompletionTokens.ToString("0.0"));
        }
        AnsiConsole.Write(table);

        return new ModelResults
        {
            Model = model,
            Results = results.OrderBy(r => r.TaskName).ThenBy(r => r.FormatDisplay).ToList(),
            Summary = summary
        };
    }

    private static string EnsureResultsDir()
    {
        var root = GetRepoRoot();
        var dir = Path.Combine(root, "benchmarks", "AIDotNet.Toon.ModelBenchmarks", "results");
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
        return Directory.GetCurrentDirectory();
    }

    private static BenchmarkFormat ParseFormat(string display)
    {
        return display.ToLowerInvariant() switch
        {
            "toon" => BenchmarkFormat.Toon,
            "json" => BenchmarkFormat.JsonPretty,
            "json compact" => BenchmarkFormat.JsonCompact,
            "yaml" => BenchmarkFormat.Yaml,
            _ => BenchmarkFormat.JsonPretty
        };
    }

    private static string[] GetModels()
    {
        // Prefer OPENAI_MODELS as comma-separated list; fallback to OPENAI_MODEL single value
        var list = Environment.GetEnvironmentVariable("OPENAI_MODELS");
        if (!string.IsNullOrWhiteSpace(list))
        {
            return list
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        var single = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
        return new[] { single };
    }

    private static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var arr = s.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(arr);
    }



    private static int GetRuns()
    {
        var v = Environment.GetEnvironmentVariable("BENCHMARK_RUNS");
        if (int.TryParse(v, out var n))
        {
            return Math.Clamp(n, 1, 10);
        }
        return 1;
    }

    private static int GetParallelism()
    {
        var v = Environment.GetEnvironmentVariable("BENCHMARK_PARALLELISM");
        if (int.TryParse(v, out var n))
        {
            return Math.Clamp(n, 1, 16);
        }
        // default moderate concurrency to speed up without being too aggressive
        return 6;
    }

    private static int GetModelParallelism()
    {
        var v = Environment.GetEnvironmentVariable("BENCHMARK_MODEL_PARALLELISM");
        if (int.TryParse(v, out var n))
        {
            return Math.Clamp(n, 1, 8);
        }
        return 2; // reasonable default
    }
}
