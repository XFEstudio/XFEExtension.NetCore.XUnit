using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using XFEExtension.NetCore.XUnit.Analyzer.CodeFix;
using XFEExtension.NetCore.XUnit.Analyzer.Diagnostics;
using XFEExtension.NetCore.XUnit.Analyzer.Generator;
using XFEExtension.NetCore.XUnit.Assertions;
using XFEExtension.NetCore.XUnit.Benchmarking;
using XFEExtension.NetCore.XUnit.Execution;
using XFEExtension.NetCore.XUnit.Runtime;

[assembly: XFEExtension.NetCore.XUnit.Attributes.UseExtension(typeof(XFEExtension.NetCore.XUnit.Test.TrackingActivator))]

namespace XFEExtension.NetCore.XUnit.Test;

[TestFixture]
[Category("Unit")]
internal class Program
{
    private static bool s_beforeAll;
    private static int s_retryAttempts;
    private bool _beforeEach;

    [BeforeAll]
    public static void BeforeAll() => s_beforeAll = true;

    [BeforeEach]
    public void BeforeEach() => _beforeEach = true;

    [Test]
    public void RunsLifecycleAndCapturesOutput()
    {
        Assert.True(s_beforeAll);
        Assert.True(_beforeEach);
        Console.WriteLine("captured output");
    }

    [TestCase(1, 2, 3)]
    [TestCase(2, 3, 5)]
    public void AddsNumbers(int left, int right, int expected) => Assert.Equal(expected, left + right);

    [MemberData(nameof(AdditionData))]
    public void ReadsMemberData(int left, int right, int expected) => Assert.Equal(expected, left + right);

    public static IEnumerable<object?[]> AdditionData =>
    [
        [10, 20, 30],
        [-1, 1, 0]
    ];

    public static ITestCaseDataSource ExtensionData => new AdditionDataSource();

    [MemberData(nameof(ExtensionData))]
    public void ReadsExtensionDataSource(int left, int right, int expected) => Assert.Equal(expected, left + right);

    [Test]
    public async Task AwaitsTask()
    {
        await Task.Delay(1);
        Assert.True(_beforeEach);
    }

    [Test]
    public async ValueTask AwaitsValueTask()
    {
        await Task.Yield();
        Assert.True(_beforeEach);
    }

    [Test]
    [Retry(1)]
    public void RetriesFailures()
    {
        Assert.True(Interlocked.Increment(ref s_retryAttempts) >= 2, "The first attempt intentionally fails.");
    }

    [Test]
    public void CalculatesStatistics()
    {
        var (statistics, outliers) = BenchmarkStatisticsCalculator.Calculate([100, 101, 99, 100, 102, 10_000], 0.05);
        Assert.True(statistics.OutlierCount >= 1);
        Assert.True(outliers[^1]);
        Assert.InRange(statistics.MedianNanoseconds, 99d, 102d);
        Assert.True(BenchmarkStatisticsCalculator.HasSignificantTrend([100, 110, 120, 130, 140, 150, 160, 170]));
        Assert.False(BenchmarkStatisticsCalculator.HasSignificantTrend([100, 101, 99, 100, 101, 99, 100, 101]));
    }

    [Test]
    public async Task CalibratesAndSubtractsOverheadWithFakeClock()
    {
        var durations = new long[]
        {
            1_000, 10_000,
            11_000, 11_000, 11_000, 11_000, 11_000, 11_000,
            1_000, 1_000, 1_000, 1_000, 1_000,
            11_000, 11_000
        };
        var descriptor = new BenchmarkDescriptor
        {
            Id = "fake-clock",
            DisplayName = "fake-clock",
            TypeName = nameof(Program),
            MethodName = nameof(CalibratesAndSubtractsOverheadWithFakeClock),
            Factory = static () => null,
            Invoker = static (_, _) => new ValueTask<object?>((object?)42),
            OverheadInvoker = static (_, _) => new ValueTask<object?>((object?)null)
        };
        var job = new BenchmarkJob
        {
            TargetIterationMilliseconds = 1,
            MinWarmupCount = 6,
            MaxWarmupCount = 6,
            MinIterationCount = 2,
            MaxIterationCount = 2,
            MaxRelativeError = 0.01,
            MeasureMemory = false,
            AllowUnsafeEnvironment = true
        };

        var result = await new BenchmarkEngine(new ScriptedBenchmarkClock(durations)).RunAsync(descriptor, job);

        Assert.Equal(2, result.Measurements.Count);
        Assert.All(result.Measurements, measurement => Assert.Equal(10L, measurement.Operations));
        Assert.InRange(result.Statistics.MeanNanoseconds, 99_999d, 100_001d);
        Assert.True(result.Statistics.Converged);
    }

    [Test]
    public void ThrowsAssertionExceptions() => Assert.Throws<XFEAssertionException>(() => Assert.Equal(1, 2));

    [Test]
    public void SerializesConsoleLanguageAsText()
    {
        var json = JsonSerializer.Serialize(new XfeRunSettings { Language = ConsoleLanguage.Chinese }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restored = JsonSerializer.Deserialize<XfeRunSettings>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"language\":\"Chinese\"", json);
        Assert.Equal(ConsoleLanguage.Chinese, restored!.Language);
    }

    [Test]
    public void UsesConfiguredActivator() => Assert.True(TrackingActivator.ActivationCount > 0);

    [Test]
    public void RegistersSmTestAsDefaultSingleRunTest()
    {
        var registry = XfeGeneratedRegistry.Create();
        var descriptor = Assert.Single(registry.Tests.Where(static test => test.MethodName == nameof(LegacyCompatibilityTests.RunsLegacyBenchmark)));

        Assert.True(descriptor.IsLegacySingleRun);
        Assert.False(registry.Benchmarks.Any(static benchmark => benchmark.MethodName == nameof(LegacyCompatibilityTests.RunsLegacyBenchmark)));
    }

    [Test]
    [Skip("Verifies skip reporting.")]
    public void SkippedTest() => throw new InvalidOperationException("A skipped test must not run.");

    [Test]
    [Explicit("Used only to verify non-zero failure exit codes.")]
    public void ExplicitFailure() => Assert.True(false, "Intentional explicit failure.");
}

[TestFixture]
internal sealed class IsolatedTests
{
    [Test]
    [Timeout(2_000)]
    public async Task HardTimeoutWorkerCompletes()
    {
        await Task.Delay(10);
        Assert.True(true);
    }
}

[TestFixture]
internal sealed class WorkerProcessTests
{
    [Test]
    public async Task RunsSmTestWithoutModeArgumentsAndShowsAllConsoleOutput()
    {
        using var process = StartCurrentProcess("--filter", nameof(LegacyCompatibilityTests.RunsLegacyBenchmark), "--report", "none", "--language", "en");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var errorOutput = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        var output = await standardOutput + await errorOutput;

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(1, CountOccurrences(output, "SMTest standard output"));
        Assert.Equal(1, CountOccurrences(output, "SMTest error output"));
        Assert.Contains("SMTest single-run output", output);
        Assert.False(output.Contains("Benchmark results", StringComparison.Ordinal));
    }

    [Test]
    public async Task TerminatesHardTimeoutWorker()
    {
        var exitCode = await XFERunner.RunAsync(["--tests", "--explicit", "--filter", nameof(HardTimeoutProbe), "--report", "none"]);
        Assert.Equal(XFERunner.TestFailureExitCode, exitCode);
    }

    [Test]
    public async Task ReportsCrashedWorker()
    {
        var exitCode = await XFERunner.RunAsync(["--tests", "--explicit", "--filter", nameof(WorkerCrashProbe), "--report", "none"]);
        Assert.Equal(XFERunner.WorkerCrashExitCode, exitCode);
    }

    [Test]
    [Explicit("Selected by TerminatesHardTimeoutWorker to validate process termination.")]
    [Timeout(100)]
    public async Task HardTimeoutProbe() => await Task.Delay(Timeout.InfiniteTimeSpan);

    [Test]
    [Explicit("Selected by ReportsCrashedWorker to validate crash reporting.")]
    [Isolated]
    public void WorkerCrashProbe() => Environment.FailFast("Intentional worker crash probe.");

    private static Process StartCurrentProcess(params string[] arguments)
    {
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the current executable path.");
        var startInfo = new ProcessStartInfo(processPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Environment.CurrentDirectory
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            startInfo.ArgumentList.Add(Assembly.GetEntryAssembly()?.Location ?? throw new InvalidOperationException("Cannot determine the entry assembly."));
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the current test process.");
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var startIndex = 0;
        while ((startIndex = value.IndexOf(search, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += search.Length;
        }
        return count;
    }
}

[TestFixture]
internal sealed class CompilerIntegrationTests
{
    [Test]
    public void GeneratorUsesSemanticDiscoveryAndDirectCalls()
    {
        const string source = """
            using Check = XFEExtension.NetCore.XUnit.Attributes.TestAttribute;
            internal sealed class AliasFixture
            {
                [Check]
                internal int AliasTest() => 42;
            }
            """;
        var compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new XUnitCodeGenerator().AsSourceGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var runResult = driver.GetRunResult();
        var generated = string.Join(Environment.NewLine, runResult.GeneratedTrees.Select(static tree => tree.GetText().ToString()));

        Assert.False(runResult.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.False(output.GetDiagnostics().Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains("AliasTest", generated);
        Assert.False(generated.Contains("MethodInfo.Invoke", StringComparison.Ordinal));
    }

    [Test]
    public async Task AnalyzerRejectsAsyncVoidAndReportsMigrationDiagnostics()
    {
        const string source = """
            using System.Threading.Tasks;
            using XFEExtension.NetCore.XUnit.Attributes;
            internal sealed class BrokenFixture
            {
                [Test]
                internal async void AsyncVoidTest() => await Task.Yield();

                [BeforeAll]
                internal void InvalidBeforeAll(int value) { }

                [MTest]
                internal void LegacyTest() { }
            }
            """;
        var compilation = CreateCompilation(source);
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new XUnitCodeAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.Contains(XUnitCodeAnalyzer.AsyncVoidId, diagnostics.Select(static diagnostic => diagnostic.Id));
        Assert.Contains(XUnitCodeAnalyzer.LifecycleId, diagnostics.Select(static diagnostic => diagnostic.Id));
        Assert.Contains(XUnitCodeAnalyzer.LegacyId, diagnostics.Select(static diagnostic => diagnostic.Id));
    }

    [Test]
    public async Task CodeFixChangesAsyncVoidToAwaitableTask()
    {
        const string source = """
            using System.Threading.Tasks;
            using XFEExtension.NetCore.XUnit.Attributes;
            internal sealed class BrokenFixture
            {
                [Test]
                internal async void AsyncVoidTest() => await Task.Yield();
            }
            """;
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "CodeFixTest", "CodeFixTest", LanguageNames.CSharp,
                parseOptions: CSharpParseOptions.Default,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)))
            .AddMetadataReferences(projectId, CreateReferences())
            .AddDocument(documentId, "BrokenFixture.cs", SourceText.From(source));
        var document = solution.GetDocument(documentId) ?? throw new InvalidOperationException("Test document was not created.");
        var compilation = await document.Project.GetCompilationAsync() ?? throw new InvalidOperationException("Test compilation was not created.");
        var diagnostic = (await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new XUnitCodeAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()).Single(item => item.Id == XUnitCodeAnalyzer.AsyncVoidId);
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);

        await new XUnitCodeFixProvider().RegisterCodeFixesAsync(context);
        var operation = (await Assert.Single(actions).GetOperationsAsync(CancellationToken.None)).OfType<ApplyChangesOperation>().Single();
        var updated = await (operation.ChangedSolution.GetDocument(documentId) ?? throw new InvalidOperationException("Updated test document was not found."))
            .GetTextAsync();

        Assert.Contains("global::System.Threading.Tasks.Task AsyncVoidTest", updated.ToString());
    }

    [Test]
    public void GeneratorReportsInvalidArgumentsAndEntryPointConflict()
    {
        const string source = """
            using XFEExtension.NetCore.XUnit.Attributes;
            internal static class Program
            {
                internal static void Main() { }

                [TestCase(1)]
                internal static void InvalidArguments(int left, int right) { }
            }
            """;
        var compilation = CreateCompilation(source, OutputKind.ConsoleApplication);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new XUnitCodeGenerator().AsSourceGenerator());

        driver = driver.RunGenerators(compilation);
        var ids = driver.GetRunResult().Diagnostics.Select(static diagnostic => diagnostic.Id).ToArray();

        Assert.Contains("XFE1002", ids);
        Assert.Contains("XFE1003", ids);
    }

    private static CSharpCompilation CreateCompilation(string source, OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary)
    {
        return CSharpCompilation.Create(
            $"XfeCompilerTest_{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(source)],
            CreateReferences(),
            new CSharpCompilationOptions(outputKind, optimizationLevel: OptimizationLevel.Release));
    }

    private static IReadOnlyList<MetadataReference> CreateReferences()
    {
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) ?? [];
        var references = trustedAssemblies.Select(static path => MetadataReference.CreateFromFile(path)).ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(Attributes.TestAttribute).Assembly.Location));
        return references;
    }
}

internal sealed class SampleFixture
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed class TrackingActivator : ITestActivator
{
    private static int s_activationCount;
    public static int ActivationCount => Volatile.Read(ref s_activationCount);

    public object? CreateInstance(Type type)
    {
        try
        {
            var instance = Activator.CreateInstance(type, true);
            if (instance is not null)
                Interlocked.Increment(ref s_activationCount);
            return instance;
        }
        catch (MissingMethodException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync(object? instance)
    {
        if (instance is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (instance is IDisposable disposable)
            disposable.Dispose();
    }
}

internal sealed class AdditionDataSource : ITestCaseDataSource
{
    public IEnumerable<object?[]> GetData()
    {
        yield return [3, 4, 7];
        yield return [10, -3, 7];
    }
}

[TestFixture]
internal sealed class FixtureTests(SampleFixture fixture) : IClassFixture<SampleFixture>
{
    [Test]
    public void InjectsFixture() => Assert.NotEqual(Guid.Empty, fixture.Id);
}

[TestFixture]
internal sealed class BenchmarkComparisonTests
{
    [Test]
    [Explicit("Release validation against BenchmarkDotNet; intentionally excluded from normal CI runs.")]
    public async Task ComparesRatioWithBenchmarkDotNet()
    {
        var descriptors = XfeGeneratedRegistry.Create().Benchmarks
            .Where(descriptor => descriptor.TypeName == typeof(ReferenceComparisonBenchmarks).FullName)
            .ToDictionary(descriptor => descriptor.MethodName);
        var job = BenchmarkJob.Quick();
        job.TargetIterationMilliseconds = 100;
        job.MinIterationCount = 8;
        job.MaxIterationCount = 15;
        job.MeasureMemory = false;
        var engine = new BenchmarkEngine();
        var ownBaseline = await engine.RunAsync(descriptors[nameof(ReferenceComparisonBenchmarks.Baseline)], job);
        var ownCandidate = await engine.RunAsync(descriptors[nameof(ReferenceComparisonBenchmarks.Candidate)], job);
        var ownRatio = ownCandidate.Statistics.MeanNanoseconds / ownBaseline.Statistics.MeanNanoseconds;

        var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<ReferenceComparisonBenchmarks>(
            BenchmarkDotNet.Configs.ManualConfig.Create(BenchmarkDotNet.Configs.DefaultConfig.Instance)
                .AddJob(BenchmarkDotNet.Jobs.Job.ShortRun));
        var bdnBaseline = summary.Reports.Single(report => report.BenchmarkCase.Descriptor.WorkloadMethod.Name == nameof(ReferenceComparisonBenchmarks.Baseline)).ResultStatistics?.Mean
            ?? throw new InvalidOperationException("BenchmarkDotNet did not return baseline statistics.");
        var bdnCandidate = summary.Reports.Single(report => report.BenchmarkCase.Descriptor.WorkloadMethod.Name == nameof(ReferenceComparisonBenchmarks.Candidate)).ResultStatistics?.Mean
            ?? throw new InvalidOperationException("BenchmarkDotNet did not return candidate statistics.");
        var bdnRatio = bdnCandidate / bdnBaseline;

        Assert.True(ownRatio > 2);
        Assert.True(bdnRatio > 2);
        Assert.True(Math.Abs(ownRatio - bdnRatio) / bdnRatio < 0.35,
            $"XFE ratio {ownRatio:F3} differs from BenchmarkDotNet ratio {bdnRatio:F3} by more than 35%.");
    }
}

public class ReferenceComparisonBenchmarks
{
    [Benchmark(Baseline = true)]
    [BenchmarkDotNet.Attributes.Benchmark(Baseline = true)]
    public int Baseline() => Work(10_000);

    [Benchmark]
    [BenchmarkDotNet.Attributes.Benchmark]
    public int Candidate() => Work(40_000);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int Work(int count)
    {
        var value = 17;
        for (var index = 0; index < count; index++)
            value = unchecked(value * 31 + index);
        return value;
    }
}

internal sealed class BenchmarkSamples
{
    [Params(16, 64)]
    public int Count { get; set; }

    [Benchmark(Baseline = true)]
    public int Baseline()
    {
        var sum = 0;
        for (var index = 0; index < Count; index++)
            sum += index;
        return sum;
    }

    [Benchmark]
    public int Candidate()
    {
        var sum = 0;
        var index = 0;
        while (index < Count)
            sum += index++;
        return sum;
    }

    [Benchmark]
    [Arguments(32)]
    public int WithArguments(int count)
    {
        var sum = 0;
        for (var index = 0; index < count; index++)
            sum += index * 2;
        return sum;
    }

    [Benchmark(Strategy = BenchmarkStrategy.ColdStart)]
    public int ColdStart() => Count + Environment.TickCount;

    [Benchmark(Strategy = BenchmarkStrategy.Monitoring)]
    public int Monitoring()
    {
        Thread.SpinWait(1_000);
        return Count;
    }
}
