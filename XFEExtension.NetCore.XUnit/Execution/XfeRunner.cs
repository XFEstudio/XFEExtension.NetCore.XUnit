using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using XFEExtension.NetCore.XUnit.Attributes;
using XFEExtension.NetCore.XUnit.Benchmarking;
using XFEExtension.NetCore.XUnit.Reporting;
using XFEExtension.NetCore.XUnit.Runtime;

namespace XFEExtension.NetCore.XUnit.Execution;

/// <summary>
/// 提供普通测试和基准的命令行入口、筛选、工作进程编排、报告及退出码映射。
/// </summary>
public static partial class XFERunner
{
    /// <summary>
    /// 表示全部已选择工作成功且未检测到性能回归的退出码。
    /// </summary>
    public const int SuccessExitCode = 0;

    /// <summary>
    /// 表示普通测试失败、测试超时或统计显著性能回归的退出码。
    /// </summary>
    public const int TestFailureExitCode = 1;

    /// <summary>
    /// 表示命令行、设置文件、发现结果或基线配置无效的退出码。
    /// </summary>
    public const int ConfigurationErrorExitCode = 2;

    /// <summary>
    /// 表示测试或基准工作进程崩溃的退出码。
    /// </summary>
    public const int WorkerCrashExitCode = 3;

    /// <summary>
    /// 表示用户或调用方取消运行的退出码。
    /// </summary>
    public const int CancelledExitCode = 4;

    /// <summary>
    /// 使用当前程序集由源码生成器注册的测试和基准执行命令行请求。
    /// </summary>
    /// <param name="args">不包含可执行文件路径的 XFE 命令行参数。</param>
    /// <param name="cancellationToken">用于取消测试、基准、工作进程等待和报告写入的令牌。</param>
    /// <returns>表示运行完成的任务；结果为 <see cref="SuccessExitCode"/> 等标准退出码之一。</returns>
    public static Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default) =>
        RunAsync(args, XfeGeneratedRegistry.Create(), cancellationToken);

    /// <summary>
    /// 使用调用方提供的注册表执行命令行请求，适用于自定义入口点和嵌入式运行场景。
    /// </summary>
    /// <param name="args">不包含可执行文件路径的 XFE 命令行参数。</param>
    /// <param name="registry">包含待筛选测试和基准描述符的注册表。</param>
    /// <param name="cancellationToken">用于取消测试、基准、工作进程等待和报告写入的令牌。</param>
    /// <returns>表示运行完成的任务；结果为 <see cref="SuccessExitCode"/> 等标准退出码之一。</returns>
    public static async Task<int> RunAsync(string[] args, XfeRegistry registry, CancellationToken cancellationToken = default)
    {
        ConsolePresenter.ConfigureConsole();
        var presenter = new ConsolePresenter(new ConsoleLocalizer(ConsoleLanguage.Auto));
        try
        {
            var options = RunnerOptions.Parse(args);
            presenter = new ConsolePresenter(new ConsoleLocalizer(options.Language ?? ConsoleLanguage.Auto));
            if (options.Error is not null)
            {
                presenter.PrintError(options.GetError(options.Language ?? ConsoleLanguage.Auto));
                return ConfigurationErrorExitCode;
            }
            if (options.ShowHelp)
            {
                presenter.PrintHelp();
                return SuccessExitCode;
            }
            if (options.ShowVersion)
            {
                presenter.PrintVersion();
                return SuccessExitCode;
            }
            var activators = LoadExtensions<ITestActivator>().ToArray();
            if (activators.Length > 1)
                throw new XFEConfigurationException("Only one ITestActivator extension can be registered per test assembly.");
            using var activatorScope = XfeObjectFactory.UseActivator(activators.SingleOrDefault());
            if (options.WorkerTestId is not null)
                return await RunTestWorkerAsync(options, registry, cancellationToken).ConfigureAwait(false);
            if (options.WorkerBenchmarkId is not null)
                return await RunBenchmarkWorkerAsync(options, registry, cancellationToken).ConfigureAwait(false);

            var settings = await LoadSettingsAsync(options.SettingsPath, cancellationToken).ConfigureAwait(false);
            options.Apply(settings);
            presenter = new ConsolePresenter(new ConsoleLocalizer(settings.Language));
            var tests = FilterTests(registry.Tests, options).ToArray();
            var benchmarks = FilterBenchmarks(registry.Benchmarks, options).ToArray();
            presenter.PrintHeader(options.ListOnly || options.RunTests, options.ListOnly || options.RunBenchmarks, tests.Length, benchmarks.Length, settings);
            if (options.ListOnly)
            {
                presenter.PrintList(tests, benchmarks);
                return SuccessExitCode;
            }

            using var cancelSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ConsoleCancelEventHandler handler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancelSource.Cancel();
            };
            Console.CancelKeyPress += handler;
            try
            {
                var exitCode = SuccessExitCode;
                if (options.RunTests)
                {
                    var testSummary = await RunTestsAsync(tests, settings, cancelSource.Token).ConfigureAwait(false);
                    presenter.PrintTests(testSummary);
                    await BuiltInReporters.WriteTestsAsync(testSummary, settings.Reports, cancelSource.Token).ConfigureAwait(false);
                    foreach (var reporter in LoadExtensions<ITestReporter>())
                        await reporter.ReportAsync(testSummary, settings.Reports.ArtifactsPath, cancelSource.Token).ConfigureAwait(false);
                    if (testSummary.Results.Any(static result => result.Outcome == TestOutcome.Crashed))
                        exitCode = WorkerCrashExitCode;
                    else if (testSummary.Failed > 0 && exitCode == SuccessExitCode)
                        exitCode = TestFailureExitCode;
                }
                if (options.RunBenchmarks)
                {
                    var benchmarkSummary = await RunBenchmarksAsync(benchmarks, settings, options, presenter, cancelSource.Token).ConfigureAwait(false);
                    presenter.PrintBenchmarks(benchmarkSummary);
                    await BuiltInReporters.WriteBenchmarksAsync(benchmarkSummary, settings.Reports, cancelSource.Token).ConfigureAwait(false);
                    foreach (var exporter in LoadExtensions<IBenchmarkExporter>())
                        await exporter.ExportAsync(benchmarkSummary, settings.Reports.ArtifactsPath, cancelSource.Token).ConfigureAwait(false);
                    if (benchmarkSummary.RegressionDetected && exitCode == SuccessExitCode)
                        exitCode = TestFailureExitCode;
                }
                if (settings.Reports.Json || settings.Reports.JUnit || settings.Reports.Markdown || settings.Reports.Csv)
                    presenter.PrintArtifacts(settings.Reports.ArtifactsPath);
                return exitCode;
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }
        }
        catch (OperationCanceledException)
        {
            presenter.PrintCancellation();
            return CancelledExitCode;
        }
        catch (XFEConfigurationException exception)
        {
            presenter.PrintError(exception.Message);
            return ConfigurationErrorExitCode;
        }
        catch (JsonException exception)
        {
            presenter.PrintError(exception.Message);
            return ConfigurationErrorExitCode;
        }
        catch (Exception exception)
        {
            presenter.PrintError(exception.ToString());
            return WorkerCrashExitCode;
        }
    }

    private static async Task<TestRunSummary> RunTestsAsync(TestDescriptor[] tests, XfeRunSettings settings, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();
        if (settings.Tests.Seed is { } seed)
            tests = tests.OrderBy(test => StableOrder(test.Id, seed)).ToArray();
        var normal = tests.Where(static test => !test.Isolated && test.TimeoutMilliseconds <= 0).ToArray();
        var isolated = tests.Except(normal).ToArray();
        var executor = new TestExecutor();
        var results = new List<TestCaseResult>(await executor.RunAsync(normal, settings.Tests, false, cancellationToken).ConfigureAwait(false));
        foreach (var test in isolated)
        {
            if (test.SkipReason is not null || test.Explicit && !settings.Tests.IncludeExplicit)
            {
                results.Add(new TestCaseResult(test.Id, test.DisplayName, TestOutcome.Skipped, TimeSpan.Zero, TimeSpan.Zero, 0,
                    test.SkipReason ?? "Explicit test was not selected.")
                {
                    IsLegacySingleRun = test.IsLegacySingleRun,
                    TypeName = test.TypeName,
                    MethodName = test.MethodName
                });
                continue;
            }
            results.Add(await WorkerProcess.RunTestAsync(test, settings.Tests, cancellationToken).ConfigureAwait(false));
        }
        watch.Stop();
        return new TestRunSummary { StartedAt = started, Duration = watch.Elapsed, Results = results.OrderBy(static item => item.DisplayName).ToArray() };
    }

    private static uint StableOrder(string value, int seed)
    {
        unchecked
        {
            var hash = 2166136261u ^ (uint)seed;
            foreach (var character in value)
                hash = (hash ^ character) * 16777619u;
            return hash;
        }
    }

    private static async Task<BenchmarkRunSummary> RunBenchmarksAsync(BenchmarkDescriptor[] benchmarks, XfeRunSettings settings, RunnerOptions options, ConsolePresenter presenter, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();
        var summaries = new List<BenchmarkSummary>();
        foreach (var benchmark in benchmarks)
        {
            BenchmarkSummary? aggregate = null;
            var launchCount = benchmark.Strategy == Attributes.BenchmarkStrategy.ColdStart
                ? settings.Benchmark.MinIterationCount
                : settings.Benchmark.MaxLaunchCount;
            for (var launch = 0; launch < launchCount; launch++)
            {
                var current = await WorkerProcess.RunBenchmarkAsync(benchmark, settings.Benchmark, cancellationToken).ConfigureAwait(false);
                current = WithLaunchIndex(current, launch);
                aggregate = aggregate is null ? current : Aggregate(aggregate, current, settings.Benchmark);
                if (benchmark.Strategy != Attributes.BenchmarkStrategy.ColdStart && aggregate.Statistics.Converged)
                    break;
            }
            summaries.Add(aggregate!);
        }

        foreach (var typeGroup in benchmarks.GroupBy(static item => item.TypeName))
        {
            foreach (var descriptor in typeGroup)
            {
                var baselineDescriptor = typeGroup.FirstOrDefault(item => item.Baseline && item.ParameterKey == descriptor.ParameterKey);
                if (baselineDescriptor is null)
                    continue;
                var baseline = summaries.FirstOrDefault(item => item.Id == baselineDescriptor.Id);
                if (baseline is null || baseline.Statistics.MeanNanoseconds <= 0)
                    continue;
                var summary = summaries.First(item => item.Id == descriptor.Id);
                summary.BaselineRatio = summary.Statistics.MeanNanoseconds / baseline.Statistics.MeanNanoseconds;
            }
        }

        var regression = false;
        if (options.BaselinePath is not null)
        {
            if (!File.Exists(options.BaselinePath))
                throw new XFEConfigurationException($"Benchmark baseline file was not found: {options.BaselinePath}");
            var baseline = BuiltInReporters.Deserialize<BenchmarkRunSummary>(await File.ReadAllTextAsync(options.BaselinePath, cancellationToken).ConfigureAwait(false));
            if (baseline is null)
                throw new XFEConfigurationException("The benchmark baseline file is invalid.");
            var errors = new List<string>();
            regression = RegressionGate.Apply(summaries, baseline, options.MaxRegression, options.AllowEnvironmentMismatch, errors);
            foreach (var error in errors)
                presenter.PrintError(error);
            if (errors.Count > 0 && !options.AllowEnvironmentMismatch)
                throw new XFEConfigurationException("Benchmark regression gating was refused because the environments differ.");
        }
        watch.Stop();
        return new BenchmarkRunSummary { StartedAt = started, Duration = watch.Elapsed, Benchmarks = summaries, RegressionDetected = regression };
    }

    private static BenchmarkSummary WithLaunchIndex(BenchmarkSummary summary, int launchIndex) => new()
    {
        Id = summary.Id,
        DisplayName = summary.DisplayName,
        Statistics = summary.Statistics,
        Gc = summary.Gc,
        Environment = summary.Environment,
        Measurements = summary.Measurements.Select(measurement => measurement with { LaunchIndex = launchIndex }).ToArray(),
        Warnings = summary.Warnings,
        BaselineRatio = summary.BaselineRatio
    };

    private static BenchmarkSummary Aggregate(BenchmarkSummary left, BenchmarkSummary right, BenchmarkJob job)
    {
        var raw = left.Measurements.Concat(right.Measurements).ToArray();
        var leftLaunchCount = Math.Max(1, left.Measurements.Select(static measurement => measurement.LaunchIndex).Distinct().Count());
        var rightLaunchCount = Math.Max(1, right.Measurements.Select(static measurement => measurement.LaunchIndex).Distinct().Count());
        var (statistics, outliers) = BenchmarkStatisticsCalculator.Calculate(raw.Select(static measurement => measurement.NanosecondsPerOperation).ToArray(), job.MaxRelativeError);
        var measurements = raw.Select((measurement, index) => measurement with { IsOutlier = index < outliers.Length && outliers[index] }).ToArray();
        var warnings = left.Warnings.Concat(right.Warnings)
            .Where(static warning => !warning.StartsWith("NotConverged", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).ToList();
        if (!statistics.Converged)
            warnings.Add($"NotConverged: relative error {statistics.RelativeError:P2} exceeded target {job.MaxRelativeError:P2} after multiple launches.");
        if (statistics.MeanNanoseconds > 0 && statistics.StandardDeviationNanoseconds / statistics.MeanNanoseconds > 0.10 &&
            !warnings.Contains("HighNoise: standard deviation exceeds 10% of the mean.", StringComparer.Ordinal))
            warnings.Add("HighNoise: standard deviation exceeds 10% of the mean.");
        var cleanSamples = measurements.Where(static measurement => !measurement.IsOutlier)
            .Select(static measurement => measurement.NanosecondsPerOperation).ToArray();
        if (BenchmarkStatisticsCalculator.HasSignificantTrend(cleanSamples) &&
            !warnings.Contains("MeasurementTrend: samples show a significant time-dependent trend.", StringComparer.Ordinal))
            warnings.Add("MeasurementTrend: samples show a significant time-dependent trend.");
        return new BenchmarkSummary
        {
            Id = left.Id,
            DisplayName = left.DisplayName,
            Statistics = statistics,
            Gc = new GcStatistics
            {
                AllocatedBytesPerOperation = WeightedAverage(left.Gc.AllocatedBytesPerOperation, leftLaunchCount, right.Gc.AllocatedBytesPerOperation, rightLaunchCount),
                Gen0CollectionsPerThousandOperations = WeightedAverage(left.Gc.Gen0CollectionsPerThousandOperations, leftLaunchCount, right.Gc.Gen0CollectionsPerThousandOperations, rightLaunchCount),
                Gen1CollectionsPerThousandOperations = WeightedAverage(left.Gc.Gen1CollectionsPerThousandOperations, leftLaunchCount, right.Gc.Gen1CollectionsPerThousandOperations, rightLaunchCount),
                Gen2CollectionsPerThousandOperations = WeightedAverage(left.Gc.Gen2CollectionsPerThousandOperations, leftLaunchCount, right.Gc.Gen2CollectionsPerThousandOperations, rightLaunchCount)
            },
            Environment = left.Environment,
            Measurements = measurements,
            Warnings = warnings
        };
    }

    private static double WeightedAverage(double left, int leftWeight, double right, int rightWeight) =>
        (left * leftWeight + right * rightWeight) / (leftWeight + rightWeight);

    private static async Task<int> RunTestWorkerAsync(RunnerOptions options, XfeRegistry registry, CancellationToken cancellationToken)
    {
        var descriptor = registry.Tests.FirstOrDefault(test => test.Id == options.WorkerTestId);
        if (descriptor is null || options.ResultPath is null || options.SettingsPath is null)
            return ConfigurationErrorExitCode;
        var settings = BuiltInReporters.Deserialize<TestRunSettings>(await File.ReadAllTextAsync(options.SettingsPath, cancellationToken).ConfigureAwait(false)) ?? new TestRunSettings();
        settings.Parallel = false;
        settings.DefaultTimeoutMilliseconds = 0;
        var results = await new TestExecutor().RunAsync([descriptor], settings, true, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(options.ResultPath, BuiltInReporters.Serialize(results[0]), cancellationToken).ConfigureAwait(false);
        return results[0].Outcome == TestOutcome.Passed || results[0].Outcome == TestOutcome.Skipped ? SuccessExitCode : TestFailureExitCode;
    }

    private static async Task<int> RunBenchmarkWorkerAsync(RunnerOptions options, XfeRegistry registry, CancellationToken cancellationToken)
    {
        var descriptor = registry.Benchmarks.FirstOrDefault(benchmark => benchmark.Id == options.WorkerBenchmarkId);
        if (descriptor is null || options.ResultPath is null || options.SettingsPath is null)
            return ConfigurationErrorExitCode;
        var job = BuiltInReporters.Deserialize<BenchmarkJob>(await File.ReadAllTextAsync(options.SettingsPath, cancellationToken).ConfigureAwait(false)) ?? new BenchmarkJob();
        var summary = await new BenchmarkEngine().RunAsync(descriptor, job, cancellationToken: cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(options.ResultPath, BuiltInReporters.Serialize(summary), cancellationToken).ConfigureAwait(false);
        return SuccessExitCode;
    }

    private static IEnumerable<TestDescriptor> FilterTests(IEnumerable<TestDescriptor> tests, RunnerOptions options) => tests.Where(test =>
        (options.Filter is null || test.DisplayName.Contains(options.Filter, StringComparison.OrdinalIgnoreCase) || test.Id.Contains(options.Filter, StringComparison.OrdinalIgnoreCase)) &&
        (options.Category is null || test.Categories.Contains(options.Category, StringComparer.OrdinalIgnoreCase)));

    private static IEnumerable<BenchmarkDescriptor> FilterBenchmarks(IEnumerable<BenchmarkDescriptor> benchmarks, RunnerOptions options) => benchmarks.Where(benchmark =>
        (options.Filter is null || benchmark.DisplayName.Contains(options.Filter, StringComparison.OrdinalIgnoreCase) || benchmark.Id.Contains(options.Filter, StringComparison.OrdinalIgnoreCase)) &&
        (options.Category is null || benchmark.Categories.Contains(options.Category, StringComparer.OrdinalIgnoreCase)));

    private static IEnumerable<T> LoadExtensions<T>() where T : class
    {
        var assembly = Assembly.GetEntryAssembly();
        if (assembly is null)
            yield break;
        foreach (var attribute in assembly.GetCustomAttributes<UseExtensionAttribute>())
        {
            if (!typeof(T).IsAssignableFrom(attribute.ExtensionType))
                continue;
            if (Activator.CreateInstance(attribute.ExtensionType) is T extension)
                yield return extension;
        }
    }

    private static async Task<XfeRunSettings> LoadSettingsAsync(string? explicitPath, CancellationToken cancellationToken)
    {
        var path = explicitPath ?? Path.Combine(Environment.CurrentDirectory, "xfe.runsettings.json");
        if (!File.Exists(path))
        {
            if (explicitPath is not null)
                throw new XFEConfigurationException($"Run settings file was not found: {path}");
            return new XfeRunSettings();
        }
        var settings = JsonSerializer.Deserialize<XfeRunSettings>(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false),
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });
        return settings ?? new XfeRunSettings();
    }

    private sealed class RunnerOptions
    {
        public bool RunTests { get; private set; } = true;
        public bool RunBenchmarks { get; private set; }
        public bool ListOnly { get; private set; }
        public string? Filter { get; private set; }
        public string? Category { get; private set; }
        public string? BaselinePath { get; private set; }
        public double MaxRegression { get; private set; } = 0.05;
        public bool AllowEnvironmentMismatch { get; private set; }
        public string? SettingsPath { get; private set; }
        public string? ResultPath { get; private set; }
        public string? WorkerTestId { get; private set; }
        public string? WorkerBenchmarkId { get; private set; }
        public string? ArtifactsPath { get; private set; }
        public int? MaxParallelism { get; private set; }
        public bool? Parallel { get; private set; }
        public bool FailFast { get; private set; }
        public bool IncludeExplicit { get; private set; }
        public bool Quick { get; private set; }
        public bool AllowUnsafeBenchmark { get; private set; }
        public int? Seed { get; private set; }
        public string? ReportFormats { get; private set; }
        public ConsoleLanguage? Language { get; private set; }
        public bool ShowHelp { get; private set; }
        public bool ShowVersion { get; private set; }
        public string? Error { get; private set; }
        private string? ChineseError { get; set; }

        public static RunnerOptions Parse(string[] args)
        {
            var options = new RunnerOptions();
            for (var i = 0; i < args.Length; i++)
            {
                string? Next()
                {
                    var option = args[i];
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                        return args[++i];
                    options.SetError($"{option} requires a value.", $"{option} 需要提供值。");
                    return null;
                }
                switch (args[i])
                {
                    case "--tests": options.RunTests = true; options.RunBenchmarks = false; break;
                    case "--benchmarks": options.RunTests = false; options.RunBenchmarks = true; break;
                    case "--all": options.RunTests = true; options.RunBenchmarks = true; break;
                    case "--list": options.ListOnly = true; break;
                    case "--help":
                    case "-h": options.ShowHelp = true; break;
                    case "--version": options.ShowVersion = true; break;
                    case "--filter": options.Filter = Next(); break;
                    case "--category": options.Category = Next(); break;
                    case "--baseline": options.BaselinePath = Next(); break;
                    case "--max-regression":
                        if (!double.TryParse(Next(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var threshold))
                            options.SetError("--max-regression requires a decimal fraction such as 0.05.", "--max-regression 需要小数比例，例如 0.05。");
                        else options.MaxRegression = threshold;
                        break;
                    case "--allow-environment-mismatch": options.AllowEnvironmentMismatch = true; break;
                    case "--settings": options.SettingsPath = Next(); break;
                    case "--artifacts": options.ArtifactsPath = Next(); break;
                    case "--parallel": options.Parallel = true; break;
                    case "--no-parallel": options.Parallel = false; break;
                    case "--max-parallel":
                        if (!int.TryParse(Next(), out var parallelism) || parallelism < 1) options.SetError("--max-parallel requires a positive integer.", "--max-parallel 需要正整数。");
                        else options.MaxParallelism = parallelism;
                        break;
                    case "--fail-fast": options.FailFast = true; break;
                    case "--explicit": options.IncludeExplicit = true; break;
                    case "--quick": options.Quick = true; break;
                    case "--allow-unsafe-benchmark": options.AllowUnsafeBenchmark = true; break;
                    case "--seed":
                        if (!int.TryParse(Next(), out var seed)) options.SetError("--seed requires an integer.", "--seed 需要整数。");
                        else options.Seed = seed;
                        break;
                    case "--report": options.ReportFormats = Next(); break;
                    case "--language":
                    case "--lang":
                        var languageValue = Next();
                        if (!ConsoleLocalizer.TryParseLanguage(languageValue, out var language))
                            options.SetError("--language requires auto, en, or zh.", "--language 需要 auto、en 或 zh。");
                        else options.Language = language;
                        break;
                    case "--xfe-worker-test": options.WorkerTestId = Next(); options.RunTests = false; break;
                    case "--xfe-worker-benchmark": options.WorkerBenchmarkId = Next(); options.RunTests = false; break;
                    case "--xfe-result": options.ResultPath = Next(); break;
                    case "--xfe-settings": options.SettingsPath = Next(); break;
                    default: options.SetError($"Unknown argument: {args[i]}", $"未知参数：{args[i]}"); break;
                }
            }
            return options;
        }

        public string GetError(ConsoleLanguage language) => new ConsoleLocalizer(language).IsChinese
            ? ChineseError ?? Error ?? string.Empty
            : Error ?? string.Empty;

        private void SetError(string english, string chinese)
        {
            Error = english;
            ChineseError = chinese;
        }

        public void Apply(XfeRunSettings settings)
        {
            if (ArtifactsPath is not null) settings.Reports.ArtifactsPath = ArtifactsPath;
            if (Language.HasValue) settings.Language = Language.Value;
            if (Parallel.HasValue) settings.Tests.Parallel = Parallel.Value;
            if (MaxParallelism.HasValue) settings.Tests.MaxParallelism = MaxParallelism.Value;
            if (FailFast) settings.Tests.FailFast = true;
            if (IncludeExplicit) settings.Tests.IncludeExplicit = true;
            if (AllowUnsafeBenchmark) settings.Benchmark.AllowUnsafeEnvironment = true;
            if (Seed.HasValue) settings.Tests.Seed = Seed;
            if (ReportFormats is not null)
            {
                var formats = ReportFormats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                settings.Reports.Json = formats.Contains("json", StringComparer.OrdinalIgnoreCase);
                settings.Reports.JUnit = formats.Contains("junit", StringComparer.OrdinalIgnoreCase);
                settings.Reports.Markdown = formats.Contains("markdown", StringComparer.OrdinalIgnoreCase) || formats.Contains("md", StringComparer.OrdinalIgnoreCase);
                settings.Reports.Csv = formats.Contains("csv", StringComparer.OrdinalIgnoreCase);
            }
            if (Quick)
            {
                var quick = BenchmarkJob.Quick();
                settings.Benchmark.TargetIterationMilliseconds = quick.TargetIterationMilliseconds;
                settings.Benchmark.MinWarmupCount = quick.MinWarmupCount;
                settings.Benchmark.MaxWarmupCount = quick.MaxWarmupCount;
                settings.Benchmark.MinIterationCount = quick.MinIterationCount;
                settings.Benchmark.MaxIterationCount = quick.MaxIterationCount;
                settings.Benchmark.MaxRelativeError = quick.MaxRelativeError;
                settings.Benchmark.MaxLaunchCount = quick.MaxLaunchCount;
            }
        }
    }
}
