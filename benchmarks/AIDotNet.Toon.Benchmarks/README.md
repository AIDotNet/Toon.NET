# AIDotNet.Toon.Benchmarks

参考 `toon/benchmarks` 的 TypeScript 基准，提供 .NET 版本的 Token 效率基准（Token Efficiency）。该基准用于比较相同数据在不同格式（JSON 格式化、JSON 压缩、TOON）下的“近似”token 数量，并生成与 TS 版相似的 Markdown 报告。

当前实现包含：
- Token Efficiency（已实现）
- Retrieval Accuracy（尚未实现：TS 版依赖多家 LLM 提供方与速率限制，计划后续在 .NET 中补齐一版可配置的运行器）


## 运行

（无需加入解决方案也可直接运行）

```bash
dotnet run -c Release --project benchmarks/AIDotNet.Toon.Benchmarks
```

运行后输出两部分：
- 控制台：ASCII 柱状图对比汇总
- Markdown 报告：`benchmarks/AIDotNet.Toon.Benchmarks/results/token-efficiency.md`


## 数据集与输出说明

与 TS 版保持同类示例：

- ⭐ GitHub Repositories：读取 `benchmarks/data/github-repos.json`
- 📈 Daily Analytics：伪造 180 天指标序列（与 TS 版统计特性一致）
- 🛒 E-Commerce Order：单条嵌套的订单数据

报告结构与 TS 版一致，包括：
- 汇总图（柱状图显示 TOON 相对 JSON 的节省）
- 详细示例（对 GitHub/Analytics 数据做展示用截断，token 统计基于完整数据）


## 与 TS 版差异（重要）

- Tokenizer：TS 版使用 `gpt-tokenizer`（`o200k_base`），本 .NET 版暂用基于正则的“近似”分词（字母/数字/标点粗粒度计数）。因此绝对 token 数不与 TS 版一致，但相对趋势与对比（尤其 TOON vs JSON）在同一运行内具参考意义。
- 路径：输出文件位于 `.NET` 工程自身目录：`benchmarks/AIDotNet.Toon.Benchmarks/results/token-efficiency.md`，未直接写入 `toon/benchmarks/results/`。
- 数据字段：GitHub 数据会做最小字段整形（如 owner/name 合成 `repo`）以贴近 TS 版演示字段。


## 扩展与下一步

- Retrieval Accuracy：对齐 TS 版的多模型多格式问答评测（需要抽象 LLM 客户端、并发/节流、重试与判分器）。
- 更精确 Tokenizer：可接入兼容 `tiktoken`/`gpt-tokenizer` 的 .NET 计数器或通过本地服务桥接实现。
- 增加 YAML/XML/CSV：若需要与 TS 完全同维度对比，可在 .NET 侧补全更多序列化器后扩展 formatter 注册表（当前仅 JSON/TOON）。


## 常见问题

- 运行失败：请确认仓库根目录下存在解决方案 `AIDotNet.Toon.sln`，且路径层级未被 IDE 或脚本修改。
- GitHub 数据缺失：若未检出 `toon/` 子仓库，基准仍可运行，但 GitHub 示例将为空集合，不影响其它示例与报告生成。


## 开发提示

- 可通过 `-c Release` 提升吞吐并减少抖动。
- 若需要加入解决方案方便 IDE 管理，可执行：
  ```bash
  dotnet sln add benchmarks/AIDotNet.Toon.Benchmarks/AIDotNet.Toon.Benchmarks.csproj