# AIDotNet.Toon.ModelBenchmarks

Benchmark the model accuracy across input formats: TOON, JSON (pretty), YAML, and compact JSON. Produces interactive HTML reports with charts. Supports benchmarking multiple models in one run.

## Requirements

- .NET 8 SDK
- An OpenAI-compatible API key

## Environment variables

- `OPENAI_API_KEY` (required): API key for OpenAI.
- `OPENAI_MODELS` (optional): Comma-separated model list. Example: `gpt-4o-mini,gpt-4o,gpt-4o-mini-translate`. If omitted, falls back to `OPENAI_MODEL`.
- `OPENAI_MODEL` (optional): Single model name. Defaults to `gpt-4o-mini`.
- `OPENAI_BASE_URL` (optional): Override base URL. Defaults to `https://api.token-ai.cn/v1`.
- `BENCHMARK_RUNS` (optional): Number of repetitions per task for stability (1-10). Defaults to `1`.
- `BENCHMARK_PARALLELISM` (optional): Max concurrent requests per model (1-16). Defaults to `6`.
- `BENCHMARK_MODEL_PARALLELISM` (optional): Max models running in parallel (1-8). Defaults to `2`.

PowerShell example (Windows):

```powershell
$env:OPENAI_API_KEY = "sk-..."

# multiple models
$env:OPENAI_MODELS = "gpt-4o-mini,gpt-4o"

# or single model
$env:OPENAI_MODEL = "gpt-4o-mini"

# optional: increase runs and parallelism
$env:BENCHMARK_RUNS = "3"
$env:BENCHMARK_PARALLELISM = "6"
$env:BENCHMARK_MODEL_PARALLELISM = "2"
```

## Run

From this folder:

```powershell
dotnet run -c Release
```

Or, on Windows, you can double-click the helper script to be guided through API key and models input:

- `run-benchmark.bat` (prompts for OPENAI_API_KEY, OPENAI_MODELS/OPENAI_MODEL, OPENAI_BASE_URL, BENCHMARK_RUNS, BENCHMARK_PARALLELISM, BENCHMARK_MODEL_PARALLELISM)

Outputs will be saved under `benchmarks/AIDotNet.Toon.ModelBenchmarks/results/` relative to the repo root:


## What it does

- Defines a small suite of deterministic tasks (math, extraction, counting, sentiment, revenue).
- Formats the same input data in four formats (TOON/JSON/YAML/JSON compact).
- Sends a precise prompt to the model and scores correctness automatically.
- Aggregates accuracy, latency, and token usage and generates a Plotly-powered HTML report.
- Live progress UI in the console via Spectre.Console with one progress bar per model; each model prints a small summary table after completion.

> Notes:
> - The benchmark performs live API calls and may incur usage costs. Concurrency is limited to be gentle.
> - HTTP calls target the OpenAI Chat Completions endpoint (`/v1/chat/completions`) with temperature=0 for determinism.
> - We’ve included the OpenAI official SDK package dependency in the project. If you prefer the SDK-based request path, please confirm the exact SDK version/API surface you require, and we’ll switch the client implementation accordingly (APIs vary between releases).
