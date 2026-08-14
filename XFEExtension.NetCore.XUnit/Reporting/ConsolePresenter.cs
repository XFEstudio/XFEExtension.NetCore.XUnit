using System.Reflection;
using System.Runtime.InteropServices;
using XFEExtension.NetCore.XUnit.Runtime;

namespace XFEExtension.NetCore.XUnit.Reporting;

internal sealed class ConsolePresenter(ConsoleLocalizer text)
{
    public void PrintHeader(bool runTests, bool runBenchmarks, int testCount, int benchmarkCount, XfeRunSettings settings)
    {
        var version = typeof(ConsolePresenter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0] ?? typeof(ConsolePresenter).Assembly.GetName().Version?.ToString(3) ?? "4.0.0";
        var mode = runTests && runBenchmarks
            ? text.Select("Tests + benchmarks", "测试 + 基准")
            : runBenchmarks ? text.Select("Benchmarks", "性能基准") : text.Select("Tests", "普通测试");
        var testDescription = text.Select($"{testCount} {(testCount == 1 ? "test" : "tests")}", $"{testCount} 个测试");
        var benchmarkDescription = text.Select($"{benchmarkCount} {(benchmarkCount == 1 ? "benchmark" : "benchmarks")}", $"{benchmarkCount} 个基准");
        var discovered = runTests && runBenchmarks
            ? text.Select($"{testDescription}, {benchmarkDescription}", $"{testDescription}，{benchmarkDescription}")
            : runBenchmarks ? benchmarkDescription : testDescription;
        var parallelism = settings.Tests.Parallel
            ? text.Select($"On ({settings.Tests.MaxParallelism} workers)", $"启用（{settings.Tests.MaxParallelism} 个工作线程）")
            : text.Select("Off", "关闭");

        WriteTitle($"XFE Test Runner {version}");
        WriteMetadata(text.Select("Mode", "模式"), mode);
        WriteMetadata(text.Select("Language", "语言"), text.LanguageName);
        WriteMetadata(text.Select("Discovered", "已发现"), discovered);
        if (runTests)
            WriteMetadata(text.Select("Parallel", "并行"), parallelism);
        WriteMetadata(text.Select("Runtime", "运行时"), RuntimeInformation.FrameworkDescription);
        WriteMetadata(text.Select("Platform", "平台"), $"{RuntimeInformation.OSDescription} · {RuntimeInformation.ProcessArchitecture}");
        WriteRule('╰', '─', '╯');
        Console.WriteLine();
    }

    public void PrintList(IReadOnlyList<TestDescriptor> tests, IReadOnlyList<BenchmarkDescriptor> benchmarks)
    {
        WriteSection(text.Select("Discovered work", "发现的项目"));
        foreach (var test in tests)
            WriteListItem(text.Select("TEST", "测试"), test.Id, test.DisplayName, ConsoleColor.Cyan);
        foreach (var benchmark in benchmarks)
            WriteListItem(text.Select("BENCH", "基准"), benchmark.Id, benchmark.DisplayName, ConsoleColor.Magenta);
        if (tests.Count == 0 && benchmarks.Count == 0)
            WriteColoredLine(text.Select("  No work matched the current filters.", "  没有项目符合当前筛选条件。"), ConsoleColor.Yellow);
        Console.WriteLine();
        var testDescription = text.Select($"{tests.Count} {(tests.Count == 1 ? "test" : "tests")}", $"{tests.Count} 个测试");
        var benchmarkDescription = text.Select($"{benchmarks.Count} {(benchmarks.Count == 1 ? "benchmark" : "benchmarks")}", $"{benchmarks.Count} 个基准");
        WriteColoredLine(text.Select($"Total: {testDescription}, {benchmarkDescription}", $"总计：{testDescription}，{benchmarkDescription}"), ConsoleColor.DarkGray);
    }

    public void PrintTests(TestRunSummary summary)
    {
        WriteSection(text.Select("Test results", "测试结果"));
        if (summary.Results.Count == 0)
        {
            WriteColoredLine(text.Select("  No tests matched the current filters.", "  没有测试符合当前筛选条件。"), ConsoleColor.Yellow);
            Console.WriteLine();
            PrintTestSummary(summary);
            return;
        }

        foreach (var result in summary.Results)
        {
            var color = OutcomeColor(result.Outcome);
            var marker = result.Outcome switch
            {
                TestOutcome.Passed => "✓",
                TestOutcome.Skipped => "↷",
                TestOutcome.TimedOut => "⌛",
                _ => "✗"
            };
            WriteColored($"  {marker} {PadDisplay(text.Outcome(result.Outcome), 7)}", color);
            Console.Write($" {result.DisplayName}");
            WriteColoredLine($"  {FormatDuration(result.TotalDuration)}", ConsoleColor.DarkGray);

            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                var label = result.Outcome == TestOutcome.Skipped ? text.Select("Reason", "原因") : text.Select("Message", "消息");
                WriteDetail(label, text.Message(result.Message), result.Outcome == TestOutcome.Skipped ? ConsoleColor.DarkYellow : ConsoleColor.Red);
            }
            if (result.Attempts > 1)
                WriteDetail(text.Select("Attempts", "尝试次数"), result.Attempts.ToString(), ConsoleColor.DarkYellow);
            if (result.Outcome != TestOutcome.Passed && !string.IsNullOrWhiteSpace(result.Output))
                WriteBlock(text.Select("Captured output", "捕获的输出"), result.Output, ConsoleColor.DarkGray);
            if (result.Outcome is TestOutcome.Failed or TestOutcome.TimedOut or TestOutcome.Crashed && !string.IsNullOrWhiteSpace(result.StackTrace))
                WriteBlock(text.Select("Stack trace", "堆栈跟踪"), result.StackTrace, ConsoleColor.DarkGray);
        }

        Console.WriteLine();
        PrintTestSummary(summary);
    }

    public void PrintBenchmarks(BenchmarkRunSummary summary)
    {
        WriteSection(text.Select("Benchmark results", "基准结果"));
        if (summary.Benchmarks.Count == 0)
        {
            WriteColoredLine(text.Select("  No benchmarks matched the current filters.", "  没有基准符合当前筛选条件。"), ConsoleColor.Yellow);
            return;
        }

        var environment = summary.Benchmarks[0].Environment;
        WriteMetadata(text.Select("Environment", "环境"), $"{environment.Framework} · {environment.Architecture} · {environment.ProcessorCount} CPUs", "  ");
        WriteMetadata(text.Select("Clock", "计时器"), $"{environment.StopwatchFrequency:N0} Hz", "  ");
        Console.WriteLine();

        var consoleWidth = SafeConsoleWidth();
        var detailedTable = consoleWidth >= 120;
        var nameWidth = detailedTable ? Math.Clamp(consoleWidth - 98, 22, 52) : Math.Max(20, consoleWidth - 47);
        var widths = detailedTable
            ? new[] { nameWidth, 11, 11, 11, 11, 11, 12, 7, 8 }
            : new[] { nameWidth, 12, 12, 7, 8 };
        var headers = detailedTable
            ? new[]
            {
                text.Select("Benchmark", "基准"),
                text.Select("Mean", "均值"),
                text.Select("Error", "误差"),
                text.Select("StdDev", "标准差"),
                text.Select("Median", "中位数"),
                "P95",
                text.Select("Allocated/op", "分配/次"),
                text.Select("Ratio", "比率"),
                text.Select("Result", "状态")
            }
            : new[]
            {
                text.Select("Benchmark", "基准"),
                text.Select("Mean", "均值"),
                text.Select("Allocated/op", "分配/次"),
                text.Select("Ratio", "比率"),
                text.Select("Result", "状态")
            };
        WriteTableRow(headers, widths, ConsoleColor.Cyan);
        WriteTableRule(widths);
        foreach (var benchmark in summary.Benchmarks)
        {
            var status = benchmark.Warnings.Count == 0
                ? text.Select("OK", "正常")
                : text.Select("WARN", "警告");
            var values = detailedTable
                ? new[]
                {
                    benchmark.DisplayName,
                    BuiltInReporters.FormatNanoseconds(benchmark.Statistics.MeanNanoseconds),
                    BuiltInReporters.FormatNanoseconds(benchmark.Statistics.ErrorNanoseconds),
                    BuiltInReporters.FormatNanoseconds(benchmark.Statistics.StandardDeviationNanoseconds),
                    BuiltInReporters.FormatNanoseconds(benchmark.Statistics.MedianNanoseconds),
                    BuiltInReporters.FormatNanoseconds(benchmark.Statistics.P95Nanoseconds),
                    FormatBytes(benchmark.Gc.AllocatedBytesPerOperation),
                    benchmark.BaselineRatio?.ToString("F3") ?? "-",
                    status
                }
                : new[]
                {
                    benchmark.DisplayName,
                    BuiltInReporters.FormatNanoseconds(benchmark.Statistics.MeanNanoseconds),
                    FormatBytes(benchmark.Gc.AllocatedBytesPerOperation),
                    benchmark.BaselineRatio?.ToString("F3") ?? "-",
                    status
                };
            WriteTableRow(values, widths, benchmark.Warnings.Count == 0 ? ConsoleColor.Gray : ConsoleColor.Yellow);
        }

        foreach (var benchmark in summary.Benchmarks.Where(static benchmark => benchmark.Warnings.Count > 0))
        {
            Console.WriteLine();
            WriteColoredLine($"  ⚠ {benchmark.DisplayName}", ConsoleColor.Yellow);
            foreach (var warning in benchmark.Warnings)
                Console.WriteLine($"    {text.Warning(warning)}");
        }

        Console.WriteLine();
        WriteRule('─', '─', '─');
        var convergence = summary.Benchmarks.Count(static benchmark => benchmark.Statistics.Converged);
        var resultText = summary.RegressionDetected
            ? text.Select("Performance regression detected", "检测到性能回归")
            : text.Select("No gated regression detected", "未检测到门禁性能回归");
        WriteColoredLine(
            text.Select(
                $"{(summary.Benchmarks.Count == 1 ? "Benchmark" : "Benchmarks")}: {summary.Benchmarks.Count}  Converged: {convergence}  Duration: {FormatDuration(summary.Duration)}  {resultText}",
                $"基准：{summary.Benchmarks.Count}  已收敛：{convergence}  用时：{FormatDuration(summary.Duration)}  {resultText}"),
            summary.RegressionDetected ? ConsoleColor.Red : ConsoleColor.Green);
    }

    public void PrintArtifacts(string path)
    {
        Console.WriteLine();
        WriteColored(text.Select("Artifacts", "报告产物"), ConsoleColor.Cyan);
        Console.WriteLine($": {Path.GetFullPath(path)}");
    }

    public void PrintHelp()
    {
        WriteTitle("XFE Test Runner 4.0");
        WriteMetadata(text.Select("Usage", "用法"), text.Select("<test application> [options]", "<测试程序> [选项]"));
        WriteRule('╰', '─', '╯');
        Console.WriteLine();
        WriteHelpSection(text.Select("Run selection", "运行选择"),
            ("--tests", text.Select("Run tests only (default).", "仅运行普通测试（默认）。")),
            ("--benchmarks", text.Select("Run benchmarks only.", "仅运行性能基准。")),
            ("--all", text.Select("Run tests and benchmarks.", "运行测试和性能基准。")),
            ("--list", text.Select("List discovered work without running it.", "列出发现的项目但不执行。")),
            ("--filter <text>", text.Select("Filter by id or display name.", "按标识或显示名称筛选。")),
            ("--category <name>", text.Select("Filter by category.", "按分类筛选。")));
        WriteHelpSection(text.Select("Execution", "执行控制"),
            ("--parallel | --no-parallel", text.Select("Enable or disable parallel test classes.", "启用或关闭测试类并行。")),
            ("--max-parallel <count>", text.Select("Set maximum test parallelism.", "设置测试最大并行数。")),
            ("--fail-fast", text.Select("Stop a group after its first failure.", "同组首次失败后停止。")),
            ("--explicit", text.Select("Include explicit tests.", "包含显式测试。")),
            ("--seed <number>", text.Select("Use a stable randomized test order.", "使用稳定的随机测试顺序。")),
            ("--quick", text.Select("Use the short benchmark job.", "使用短程基准作业。")));
        WriteHelpSection(text.Select("Interface and reports", "界面与报告"),
            ("--language <auto|en|zh>", text.Select("Select UI language; auto falls back to English.", "选择界面语言；自动模式无法识别时回退英文。")),
            ("--settings <path>", text.Select("Load a runsettings JSON file.", "加载 runsettings JSON 文件。")),
            ("--report <formats>", text.Select("Select json,junit,markdown,csv or none.", "选择 json、junit、markdown、csv 或 none。")),
            ("--artifacts <path>", text.Select("Set the report output directory.", "设置报告输出目录。")),
            ("--help", text.Select("Show this help.", "显示此帮助。")),
            ("--version", text.Select("Show runner version.", "显示运行器版本。")));
        WriteHelpSection(text.Select("Performance gate", "性能门禁"),
            ("--baseline <path>", text.Select("Load a benchmark JSON baseline.", "加载基准 JSON 基线。")),
            ("--max-regression <fraction>", text.Select("Set the allowed regression, for example 0.05.", "设置允许的回归比例，例如 0.05。")),
            ("--allow-environment-mismatch", text.Select("Allow gating across different environments.", "允许在不同环境间执行门禁。")));
    }

    public void PrintVersion()
    {
        var version = typeof(ConsolePresenter).Assembly.GetName().Version?.ToString(3) ?? "4.0.0";
        Console.WriteLine($"XFE Test Runner {version}");
    }

    public void PrintError(string message)
    {
        WriteColored(text.Select("Error: ", "错误："), ConsoleColor.Red, Console.Error);
        Console.Error.WriteLine(text.Message(message));
    }

    public void PrintCancellation() => WriteColoredLine(text.Select("Run cancelled.", "运行已取消。"), ConsoleColor.Yellow, Console.Error);

    private void PrintTestSummary(TestRunSummary summary)
    {
        WriteRule('─', '─', '─');
        WriteColored(text.Select($"Total {summary.Total}", $"总计 {summary.Total}"), ConsoleColor.White);
        Console.Write("  ");
        WriteColored(text.Select($"Passed {summary.Passed}", $"通过 {summary.Passed}"), ConsoleColor.Green);
        Console.Write("  ");
        WriteColored(text.Select($"Failed {summary.Failed}", $"失败 {summary.Failed}"), summary.Failed == 0 ? ConsoleColor.DarkGray : ConsoleColor.Red);
        Console.Write("  ");
        WriteColored(text.Select($"Skipped {summary.Skipped}", $"跳过 {summary.Skipped}"), ConsoleColor.Yellow);
        Console.WriteLine(text.Select($"  Duration {FormatDuration(summary.Duration)}", $"  用时 {FormatDuration(summary.Duration)}"));
    }

    private void WriteTitle(string title)
    {
        var width = SafeConsoleWidth();
        var suffixLength = Math.Max(1, width - DisplayWidth(title) - 4);
        WriteColored($"╭─ {title} ", ConsoleColor.Cyan);
        WriteColoredLine(new string('─', suffixLength) + "╮", ConsoleColor.DarkCyan);
    }

    private static void WriteMetadata(string label, string value, string indent = "│ ")
    {
        Console.Write(indent);
        Console.Write(PadDisplay(label, 12));
        Console.WriteLine(value);
    }

    private void WriteSection(string title)
    {
        WriteColoredLine(title, ConsoleColor.Cyan);
        WriteColoredLine(new string('─', Math.Min(SafeConsoleWidth(), Math.Max(32, DisplayWidth(title) + 12))), ConsoleColor.DarkCyan);
    }

    private void WriteListItem(string kind, string id, string displayName, ConsoleColor color)
    {
        WriteColored($"  {PadDisplay(kind, 7)}", color);
        Console.WriteLine($" {displayName}  [{id}]");
    }

    private static void WriteDetail(string label, string value, ConsoleColor color)
    {
        WriteColored($"      {label}: ", color);
        Console.WriteLine(value);
    }

    private static void WriteBlock(string label, string value, ConsoleColor color)
    {
        WriteColoredLine($"      {label}:", color);
        foreach (var line in value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            Console.WriteLine($"        {line}");
    }

    private static void WriteHelpSection(string title, params (string Option, string Description)[] items)
    {
        WriteColoredLine(title, ConsoleColor.Cyan);
        foreach (var item in items)
        {
            Console.Write("  ");
            WriteColored(PadDisplay(item.Option, 34), ConsoleColor.White);
            Console.WriteLine(item.Description);
        }
        Console.WriteLine();
    }

    private static void WriteTableRow(IReadOnlyList<string> values, IReadOnlyList<int> widths, ConsoleColor color)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0)
                Console.Write("  ");
            WriteColored(PadDisplay(TruncateDisplay(values[index], widths[index]), widths[index]), color);
        }
        Console.WriteLine();
    }

    private static void WriteTableRule(IReadOnlyList<int> widths)
    {
        for (var index = 0; index < widths.Count; index++)
        {
            if (index > 0)
                Console.Write("──");
            Console.Write(new string('─', widths[index]));
        }
        Console.WriteLine();
    }

    private static void WriteRule(char left, char fill, char right)
    {
        Console.Write(left);
        Console.Write(new string(fill, Math.Max(1, SafeConsoleWidth() - 2)));
        Console.WriteLine(right);
    }

    private static ConsoleColor OutcomeColor(TestOutcome outcome) => outcome switch
    {
        TestOutcome.Passed => ConsoleColor.Green,
        TestOutcome.Skipped => ConsoleColor.Yellow,
        _ => ConsoleColor.Red
    };

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMilliseconds < 0.001)
            return $"{duration.TotalNanoseconds:F0} ns";
        if (duration.TotalMilliseconds < 1)
            return $"{duration.TotalMicroseconds:F2} μs";
        if (duration.TotalSeconds < 1)
            return $"{duration.TotalMilliseconds:F3} ms";
        return $"{duration.TotalSeconds:F3} s";
    }

    private static string FormatBytes(double bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024 * 1024):F2} MiB";
        if (bytes >= 1024)
            return $"{bytes / 1024:F2} KiB";
        return $"{bytes:F2} B";
    }

    private static int SafeConsoleWidth()
    {
        try
        {
            var width = Console.WindowWidth;
            return width > 0 ? Math.Clamp(width - 1, 96, 150) : 120;
        }
        catch
        {
            return 120;
        }
    }

    private static int DisplayWidth(string value) => value.Sum(static character => character >= 0x2E80 ? 2 : 1);

    private static string PadDisplay(string value, int width)
    {
        var padding = Math.Max(0, width - DisplayWidth(value));
        return value + new string(' ', padding);
    }

    private static string TruncateDisplay(string value, int width)
    {
        if (DisplayWidth(value) <= width)
            return value;
        var result = new System.Text.StringBuilder();
        var currentWidth = 0;
        foreach (var character in value)
        {
            var characterWidth = character >= 0x2E80 ? 2 : 1;
            if (currentWidth + characterWidth > width - 1)
                break;
            result.Append(character);
            currentWidth += characterWidth;
        }
        return result + "…";
    }

    private static void WriteColored(string value, ConsoleColor color, TextWriter? writer = null)
    {
        writer ??= Console.Out;
        var redirected = ReferenceEquals(writer, Console.Error) ? Console.IsErrorRedirected : Console.IsOutputRedirected;
        if (redirected || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))
        {
            writer.Write(value);
            return;
        }
        var original = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            writer.Write(value);
        }
        finally
        {
            Console.ForegroundColor = original;
        }
    }

    private static void WriteColoredLine(string value, ConsoleColor color, TextWriter? writer = null)
    {
        WriteColored(value, color, writer);
        (writer ?? Console.Out).WriteLine();
    }
}
