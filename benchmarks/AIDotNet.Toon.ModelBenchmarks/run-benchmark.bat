@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem Remember current code page and switch to UTF-8 to avoid garbled Chinese characters
for /f "tokens=2 delims=:." %%a in ('chcp') do set "_ORIG_CP=%%a"
chcp 65001 >nul 2>nul

echo ======================================================
echo   AIDotNet.Toon 模型基准测试 启动脚本 (Windows .bat)
echo   该脚本将引导你输入 API Key 和模型后启动程序
echo   - 支持多个模型: 以逗号分隔，例如: gpt-4o-mini,gpt-4o
echo   - 真实请求: 使用 /v1/chat/completions 接口
echo ======================================================

:: 读取 OPENAI_API_KEY （必填）
:ask_api
echo.
set "API_KEY=%OPENAI_API_KEY%"
set /p API_KEY=请输入 OPENAI_API_KEY（必填，明文显示）: 
if "%API_KEY%"=="" (
  echo [提示] API Key 不能为空，请重新输入。
  goto ask_api
)

:: 读取模型（可多选，逗号分隔）；为空则使用默认 gpt-4o-mini
echo.
set "MODELS=%OPENAI_MODELS%"
if "%MODELS%"=="" set "MODELS=%OPENAI_MODEL%"
set /p MODELS=请输入模型名称（可多个，逗号分隔；留空默认为 gpt-4o-mini）: 
if "%MODELS%"=="" set "MODELS=gpt-4o-mini"

:: 读取可选 Base URL
echo.
set "BASE_URL=%OPENAI_BASE_URL%"
set /p BASE_URL=可选：请输入 OPENAI_BASE_URL（默认 https://api.token-ai.cn/v1）: 
if "%BASE_URL%"=="" set "BASE_URL=https://api.token-ai.cn/v1"

:: 读取可选 BENCHMARK_RUNS（每个任务运行次数，默认 1）
echo.
set "RUNS=%BENCHMARK_RUNS%"
set /p RUNS=可选：每个任务运行次数 BENCHMARK_RUNS（1-10，留空为 1）: 
if "%RUNS%"=="" set "RUNS=1"

:: 读取可选 BENCHMARK_PARALLELISM（最大并发，默认 6）
echo.
set "PARA=%BENCHMARK_PARALLELISM%"
set /p PARA=可选：最大并发 BENCHMARK_PARALLELISM（1-16，留空为 6）: 
if "%PARA%"=="" set "PARA=6"

:: 读取可选 BENCHMARK_MODEL_PARALLELISM（模型级并发，默认 2）
echo.
set "MP=%BENCHMARK_MODEL_PARALLELISM%"
set /p MP=可选：模型级并发 BENCHMARK_MODEL_PARALLELISM（1-8，留空为 2）: 
if "%MP%"=="" set "MP=2"

:: 判断输入是否包含逗号，如果包含则使用 OPENAI_MODELS，否则使用 OPENAI_MODEL
set "TMP=%MODELS:,=%"
if not "%TMP%"=="%MODELS%" (
  set "OPENAI_MODELS=%MODELS%"
  set "OPENAI_MODEL="
) else (
  set "OPENAI_MODEL=%MODELS%"
  set "OPENAI_MODELS="
)

:: 设置环境变量（仅对当前进程及子进程有效）
set "OPENAI_API_KEY=%API_KEY%"
set "OPENAI_BASE_URL=%BASE_URL%"
set "BENCHMARK_RUNS=%RUNS%"
set "BENCHMARK_PARALLELISM=%PARA%"
set "BENCHMARK_MODEL_PARALLELISM=%MP%"

echo.
echo 配置预览：
echo   OPENAI_API_KEY=****（已设置）
if not "%OPENAI_MODELS%"=="" (
  echo   OPENAI_MODELS=%OPENAI_MODELS%
) else (
  echo   OPENAI_MODEL=%OPENAI_MODEL%
)
echo   OPENAI_BASE_URL=%OPENAI_BASE_URL%
echo   BENCHMARK_RUNS=%BENCHMARK_RUNS%
echo   BENCHMARK_PARALLELISM=%BENCHMARK_PARALLELISM%
echo   BENCHMARK_MODEL_PARALLELISM=%BENCHMARK_MODEL_PARALLELISM%

:: 进入基准项目目录并执行
pushd "%~dp0"
echo.
echo 正在启动基准测试（首次运行将进行构建）...
dotnet run -c Release
set ERR=%ERRORLEVEL%
popd

if not "%ERR%"=="0" (
  echo.
  echo [错误] 运行失败，错误码 %ERR%
  pause
  exit /b %ERR%
)

echo.
echo 运行结束。报告文件位于：benchmarks\AIDotNet.Toon.ModelBenchmarks\results\
echo  - 每模型一份：model-accuracy-^<model^>.html
echo  - 汇总入口：index.html

rem Restore original code page
if defined _ORIG_CP chcp %_ORIG_CP% >nul 2>nul

pause
exit /b 0
