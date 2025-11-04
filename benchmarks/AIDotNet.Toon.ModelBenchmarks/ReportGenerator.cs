using System.Text.Json;

namespace AIDotNet.Toon.ModelBenches;

internal static class ReportGenerator
{
    public static void GenerateUnifiedHtml(List<ModelResults> allModels, string outPath)
    {
        static string J(object o) => JsonSerializer.Serialize(o);
        static string HtmlEscape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh-CN\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine("  <title>模型基准测试综合报告</title>");
        sb.AppendLine("  <script src=\"https://cdn.plot.ly/plotly-2.27.0.min.js\"></script>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: system-ui, sans-serif; margin: 16px; }");
        sb.AppendLine("    .chart { width: 100%; height: 500px; }");
        sb.AppendLine("    .grid { display: grid; grid-template-columns: 1fr; gap: 24px; }");
        sb.AppendLine("    @media (min-width: 1100px) { .grid { grid-template-columns: 1fr 1fr; } }");
        sb.AppendLine("    table { border-collapse: collapse; width: 100%; margin-top: 16px; }");
        sb.AppendLine("    th, td { border: 1px solid #ddd; padding: 8px; }");
        sb.AppendLine("    th { background: #fafafa; text-align: left; }");
        sb.AppendLine("    h1 { margin-bottom: 8px; }");
        sb.AppendLine("    h2 { margin-top: 32px; margin-bottom: 16px; }");
        sb.AppendLine("    .hint { color: #666; margin-bottom: 16px; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <h1>模型基准测试综合报告</h1>");
        sb.AppendLine($"  <div class=\"hint\">测试模型：{string.Join(", ", allModels.Select(m => m.Model))} | 总样本数：{allModels.Sum(m => m.Results.Count)}</div>");
        sb.AppendLine("  <div class=\"hint\" style=\"font-size: 0.9em; margin-top: 8px;\">📊 Token（标记）：LLM API 的基本计量单位，通常 1 Token ≈ 0.75 英文单词 或 1.5-2 个中文字符。Token 数量直接影响 API 调用成本和响应速度。</div>");

        // 准确率对比
        sb.AppendLine("  <h2>准确率对比</h2>");
        sb.AppendLine("  <div class=\"grid\">");
        sb.AppendLine("    <div><h3>各模型在不同格式下的准确率</h3><div id=\"accByFormat\" class=\"chart\"></div></div>");
        sb.AppendLine("    <div><h3>各模型在不同任务上的平均准确率</h3><div id=\"accByTask\" class=\"chart\"></div></div>");
        sb.AppendLine("  </div>");

        // Token 消耗对比
        sb.AppendLine("  <h2>Token 消耗对比（计量单位：个）</h2>");
        sb.AppendLine("  <div class=\"grid\">");
        sb.AppendLine("    <div><h3>各模型的提示 Token 消耗（按格式）</h3><div id=\"promptTokens\" class=\"chart\"></div></div>");
        sb.AppendLine("    <div><h3>各模型的生成 Token 消耗（按格式）</h3><div id=\"completionTokens\" class=\"chart\"></div></div>");
        sb.AppendLine("    <div><h3>各模型的总 Token 消耗（按格式）</h3><div id=\"totalTokens\" class=\"chart\"></div></div>");
        sb.AppendLine("    <div><h3>Token 消耗分布（箱线图，按模型）</h3><div id=\"tokenBox\" class=\"chart\"></div></div>");
        sb.AppendLine("  </div>");

        // 热图分析
        sb.AppendLine("  <h2>准确率热图</h2>");
        sb.AppendLine("  <div class=\"grid\">");
        sb.AppendLine("    <div><h3>模型 × 格式 准确率热图（%）</h3><div id=\"heatModelFormat\" class=\"chart\"></div></div>");
        sb.AppendLine("    <div><h3>模型 × 任务 准确率热图（%）</h3><div id=\"heatModelTask\" class=\"chart\"></div></div>");
        sb.AppendLine("  </div>");

        // 综合数据表
        sb.AppendLine("  <h2>汇总数据表</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <thead><tr><th>模型</th><th>格式</th><th>准确率 (%)</th><th>平均提示 Token 数</th><th>平均生成 Token 数</th><th>平均总 Token 数</th></tr></thead>");
        sb.AppendLine("    <tbody>");
        foreach (var model in allModels.OrderBy(m => m.Model))
        {
            foreach (var s in model.Summary.OrderBy(s => s.FormatDisplay))
            {
                var avgTotal = s.AvgPromptTokens + s.AvgCompletionTokens;
                sb.AppendLine($"      <tr><td>{HtmlEscape(model.Model)}</td><td>{HtmlEscape(s.FormatDisplay)}</td><td>{(s.Accuracy * 100):F1}</td><td>{s.AvgPromptTokens:F1}</td><td>{s.AvgCompletionTokens:F1}</td><td>{avgTotal:F1}</td></tr>");
            }
        }
        sb.AppendLine("    </tbody>");
        sb.AppendLine("  </table>");

        sb.AppendLine();
        sb.AppendLine("  <script>");

        // 准备数据
        var modelNames = allModels.Select(m => m.Model).ToArray();
        var formats = allModels.First().Summary.Select(s => s.FormatDisplay).OrderBy(f => f).ToArray();
        var allTasks = allModels.SelectMany(m => m.Results.Select(r => r.TaskName)).Distinct().OrderBy(t => t).ToArray();

        // 准确率：模型×格式
        var accByFormat = new Dictionary<string, Dictionary<string, double>>();
        foreach (var model in allModels)
        {
            var map = new Dictionary<string, double>();
            foreach (var s in model.Summary)
                map[s.FormatDisplay] = Math.Round(s.Accuracy * 100.0, 1);
            accByFormat[model.Model] = map;
        }

        // 准确率：模型×任务
        var accByTask = new Dictionary<string, Dictionary<string, double>>();
        foreach (var model in allModels)
        {
            var map = new Dictionary<string, double>();
            foreach (var task in allTasks)
            {
                var taskResults = model.Results.Where(r => r.TaskName == task).ToList();
                double taskAcc = taskResults.Count == 0 ? 0 : taskResults.Average(r => r.Correct ? 1.0 : 0.0);
                map[task] = Math.Round(taskAcc * 100.0, 1);
            }
            accByTask[model.Model] = map;
        }

        // Token 消耗
        var promptTokensByFormat = new Dictionary<string, Dictionary<string, double>>();
        var completionTokensByFormat = new Dictionary<string, Dictionary<string, double>>();
        var totalTokensByFormat = new Dictionary<string, Dictionary<string, double>>();
        foreach (var model in allModels)
        {
            var pMap = new Dictionary<string, double>();
            var cMap = new Dictionary<string, double>();
            var tMap = new Dictionary<string, double>();
            foreach (var s in model.Summary)
            {
                pMap[s.FormatDisplay] = Math.Round(s.AvgPromptTokens, 1);
                cMap[s.FormatDisplay] = Math.Round(s.AvgCompletionTokens, 1);
                tMap[s.FormatDisplay] = Math.Round(s.AvgPromptTokens + s.AvgCompletionTokens, 1);
            }
            promptTokensByFormat[model.Model] = pMap;
            completionTokensByFormat[model.Model] = cMap;
            totalTokensByFormat[model.Model] = tMap;
        }

        // Token 箱线图
        var tokenBoxData = new Dictionary<string, List<int>>();
        foreach (var model in allModels)
            tokenBoxData[model.Model] = model.Results.Select(r => r.TotalTokens).ToList();

        // 热图数据
        var heatModelFormatZ = new List<List<double>>();
        foreach (var model in modelNames)
        {
            var row = formats.Select(fmt => accByFormat[model][fmt]).ToList();
            heatModelFormatZ.Add(row);
        }

        var heatModelTaskZ = new List<List<double>>();
        foreach (var model in modelNames)
        {
            var row = allTasks.Select(task => accByTask[model][task]).ToList();
            heatModelTaskZ.Add(row);
        }

        // 输出 JS 变量
        sb.Append("    const modelNames = "); sb.Append(J(modelNames)); sb.AppendLine(";");
        sb.Append("    const formats = "); sb.Append(J(formats)); sb.AppendLine(";");
        sb.Append("    const allTasks = "); sb.Append(J(allTasks)); sb.AppendLine(";");
        sb.Append("    const accByFormat = "); sb.Append(J(accByFormat)); sb.AppendLine(";");
        sb.Append("    const accByTask = "); sb.Append(J(accByTask)); sb.AppendLine(";");
        sb.Append("    const promptTokensByFormat = "); sb.Append(J(promptTokensByFormat)); sb.AppendLine(";");
        sb.Append("    const completionTokensByFormat = "); sb.Append(J(completionTokensByFormat)); sb.AppendLine(";");
        sb.Append("    const totalTokensByFormat = "); sb.Append(J(totalTokensByFormat)); sb.AppendLine(";");
        sb.Append("    const tokenBoxData = "); sb.Append(J(tokenBoxData)); sb.AppendLine(";");
        sb.Append("    const heatModelFormatZ = "); sb.Append(J(heatModelFormatZ)); sb.AppendLine(";");
        sb.Append("    const heatModelTaskZ = "); sb.Append(J(heatModelTaskZ)); sb.AppendLine(";");

        // Plotly 图表
        sb.AppendLine("    const accByFormatTraces = modelNames.map(m => ({ x: formats, y: formats.map(f => accByFormat[m][f]), name: m, type: 'bar' }));");
        sb.AppendLine("    Plotly.newPlot('accByFormat', accByFormatTraces, { barmode: 'group', yaxis: { title: '准确率（%）', range: [0, 100] }, xaxis: { title: '格式' }, margin: { t: 20, r: 10, l: 60, b: 80 }, legend: { orientation: 'h' } }, { responsive: true });");

        sb.AppendLine("    const accByTaskTraces = modelNames.map(m => ({ x: allTasks, y: allTasks.map(t => accByTask[m][t]), name: m, type: 'bar' }));");
        sb.AppendLine("    Plotly.newPlot('accByTask', accByTaskTraces, { barmode: 'group', yaxis: { title: '准确率（%）', range: [0, 100] }, xaxis: { title: '任务' }, margin: { t: 20, r: 10, l: 60, b: 120 }, legend: { orientation: 'h' } }, { responsive: true });");

        sb.AppendLine("    const promptTokensTraces = modelNames.map(m => ({ x: formats, y: formats.map(f => promptTokensByFormat[m][f]), name: m, type: 'bar' }));");
        sb.AppendLine("    Plotly.newPlot('promptTokens', promptTokensTraces, { barmode: 'group', yaxis: { title: '平均提示 Token 数量（个）' }, xaxis: { title: '格式' }, margin: { t: 20, r: 10, l: 80, b: 80 }, legend: { orientation: 'h' } }, { responsive: true });");

        sb.AppendLine("    const completionTokensTraces = modelNames.map(m => ({ x: formats, y: formats.map(f => completionTokensByFormat[m][f]), name: m, type: 'bar' }));");
        sb.AppendLine("    Plotly.newPlot('completionTokens', completionTokensTraces, { barmode: 'group', yaxis: { title: '平均生成 Token 数量（个）' }, xaxis: { title: '格式' }, margin: { t: 20, r: 10, l: 80, b: 80 }, legend: { orientation: 'h' } }, { responsive: true });");

        sb.AppendLine("    const totalTokensTraces = modelNames.map(m => ({ x: formats, y: formats.map(f => totalTokensByFormat[m][f]), name: m, type: 'bar' }));");
        sb.AppendLine("    Plotly.newPlot('totalTokens', totalTokensTraces, { barmode: 'group', yaxis: { title: '平均总 Token 数量（个）' }, xaxis: { title: '格式' }, margin: { t: 20, r: 10, l: 80, b: 80 }, legend: { orientation: 'h' } }, { responsive: true });");

        sb.AppendLine("    const tokenBoxTraces = modelNames.map(m => ({ y: tokenBoxData[m], name: m, type: 'box', boxmean: true }));");
        sb.AppendLine("    Plotly.newPlot('tokenBox', tokenBoxTraces, { yaxis: { title: 'Token 数量（个）' }, margin: { t: 20, r: 10, l: 80, b: 80 }, legend: { orientation: 'h' } }, { responsive: true });");

        sb.AppendLine("    Plotly.newPlot('heatModelFormat', [{ z: heatModelFormatZ, x: formats, y: modelNames, type: 'heatmap', colorscale: 'Viridis', zmin: 0, zmax: 100 }], { xaxis: { title: '格式' }, yaxis: { title: '模型' }, margin: { t: 20, r: 10, l: 100, b: 80 } }, { responsive: true });");

        sb.AppendLine("    Plotly.newPlot('heatModelTask', [{ z: heatModelTaskZ, x: allTasks, y: modelNames, type: 'heatmap', colorscale: 'Viridis', zmin: 0, zmax: 100 }], { xaxis: { title: '任务' }, yaxis: { title: '模型' }, margin: { t: 20, r: 10, l: 100, b: 120 } }, { responsive: true });");

        sb.AppendLine("  </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, sb.ToString());
    }
}

internal sealed class ModelResults
{
    public string Model { get; init; } = string.Empty;
    public List<SingleResult> Results { get; init; } = new();
    public List<FormatSummary> Summary { get; init; } = new();
}

internal sealed class SingleResult
{
    public string TaskId { get; init; } = string.Empty;
    public string TaskName { get; init; } = string.Empty;
    public string FormatDisplay { get; init; } = string.Empty;
    public bool Correct { get; init; }
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens { get; init; }
    public string Answer { get; init; } = string.Empty;
}

internal sealed class FormatSummary
{
    public BenchmarkFormat Format { get; init; }
    public string FormatDisplay { get; init; } = string.Empty;
    public double Accuracy { get; init; }
    public double AvgPromptTokens { get; init; }
    public double AvgCompletionTokens { get; init; }
}
