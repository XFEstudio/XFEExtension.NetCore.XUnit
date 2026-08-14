# XFEExtension.NetCore.XUnit 4.0

An independent .NET 10 test and statistical benchmark runner. Despite the historical package name, this project is not based on or affiliated with xUnit.net.

## Tests

```csharp
[TestFixture]
public class CalculatorTests
{
    [BeforeEach]
    public void SetUp() { }

    [TestCase(1, 2, 3)]
    [TestCase(2, 3, 5)]
    public void Adds(int left, int right, int expected)
        => Assert.Equal(expected, left + right);

    [Test]
    public async Task CompletesAsync()
        => await Task.Delay(1);
}
```

The incremental source generator discovers tests at build time, emits strongly typed invokers, and generates an entry point when the project does not already have one. `async void` tests are rejected because no runner can await their completion reliably.

Run tests directly:

```text
dotnet run -c Release
dotnet run -c Release -- --filter Calculator --category Unit
```

Different classes run in parallel by default while methods in one class remain serial. Use `[Collection]`, `[NonParallel]`, `[Timeout]`, and `[Isolated]` for shared resources and process isolation. Results are exported as console output, JSON, and JUnit XML.

## Benchmarks

```csharp
public class ParserBenchmarks
{
    [Params(10, 100)]
    public int Count { get; set; }

    [Benchmark(Baseline = true)]
    public int Baseline() => Enumerable.Range(0, Count).Sum();

    [Benchmark]
    public int Candidate() => Enumerable.Range(0, Count).Aggregate(0, (sum, value) => sum + value);
}
```

Benchmarks are opt-in and must be run from an optimized Release build:

```text
dotnet run -c Release -- --benchmarks
dotnet run -c Release -- --benchmarks --quick
```

The balanced job pilots invocation count, targets approximately 500 ms iterations, performs 6–50 warmups and 15–100 measurements, calibrates a return-shape-compatible empty workload, and stops at an approximately 2% relative error target using a 99.9% confidence interval. Raw samples, outliers, allocation, and GC counts are retained in JSON; Markdown and CSV summaries are also produced. Results below measurable infrastructure overhead are reported as such rather than presented as precise single-operation timings.

Performance gating is explicit:

```text
dotnet run -c Release -- --benchmarks --baseline previous/benchmark-results.json --max-regression 0.05
```

## Configuration

Project defaults may be stored in `xfe.runsettings.json`. Precedence is CLI, method/class/assembly attributes, configuration file, then built-in defaults.

```json
{
  "tests": { "parallel": true, "maxParallelism": 8 },
  "benchmark": { "targetIterationMilliseconds": 500, "maxRelativeError": 0.02 },
  "reports": { "artifactsPath": "XfeTestArtifacts" }
}
```

Useful commands include `--tests`, `--benchmarks`, `--all`, `--list`, `--filter`, `--category`, `--parallel`, `--no-parallel`, `--fail-fast`, `--explicit`, `--artifacts`, `--baseline`, and `--max-regression`.

## Extensions

Register one or more assembly-level `[UseExtension(typeof(...))]` attributes to add an `ITestReporter`, `IBenchmarkExporter`, or a single `ITestActivator`. A `[MemberData]` member may return either an enumerable of rows or an `ITestCaseDataSource`. The activator owns test/fixture creation and asynchronous disposal for the run.

The analyzer rejects `async void` and invalid lifecycle signatures. Migration diagnostics for the 3.x attributes include code fixes; the code-fix assembly is kept separate from the command-line analyzer/generator so Release builds do not acquire a Roslyn Workspaces dependency.

## Migrating from 3.x

`CTest`, `MTest`, `MRTest`, `SMTest`, `SetUp`, and `XFECode` remain source-compatible for the 4.x line and are marked obsolete. Migrate to `TestFixture`, `Test`, `TestCase`, `Benchmark`, `BeforeEach`, and `Assert`. Legacy timing attributes are treated as benchmarks and therefore run only with `--benchmarks`. The compatibility surface will be removed in 5.0.
