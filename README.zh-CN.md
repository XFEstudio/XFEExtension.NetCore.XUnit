# XFEExtension.NetCore.XUnit 4.0

面向 .NET 10 的独立测试与统计基准运行器。由于历史原因包名包含 XUnit，但本项目并不基于 xUnit.net，也与 xUnit.net 无隶属关系。

## 普通测试

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

增量源码生成器会在编译期发现测试、生成强类型调用器，并在项目没有入口点时自动生成入口点。`async void` 会产生编译错误，因为运行器无法可靠等待它完成。

```text
dotnet run -c Release
dotnet run -c Release -- --filter Calculator --category Unit
```

默认情况下，不同测试类并行、同一类内串行。可通过 `[Collection]`、`[NonParallel]`、`[Timeout]` 和 `[Isolated]` 管理共享资源及子进程隔离。结果会导出到控制台、JSON 和 JUnit XML。

控制台会根据当前用户界面区域自动识别语言：中文区域使用简体中文，其他及无法识别的区域默认使用英文。可随时通过 `--language en`、`--language zh` 或 `--language auto` 手动覆盖。自适应界面提供双栏运行卡片、编码安全的状态徽章、对齐的测试耗时、层级化失败详情、成功率与最慢测试汇总，以及包含环境和收敛警告的详细基准表格。重定向时保持纯文本，并支持 `NO_COLOR`。

## 性能基准

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

基准不会在普通测试中自动执行，必须在优化后的 Release 项目中显式启动：

```text
dotnet run -c Release -- --benchmarks
dotnet run -c Release -- --benchmarks --quick
```

默认均衡作业会自动校准调用次数，以约 500ms 为单轮目标，执行 6–50 轮预热和 15–100 轮实际采样，并扣除相同返回形状的空负载开销。收敛目标为约 2% 相对误差和 99.9% 置信区间。原始样本、异常值、内存分配和 GC 次数均保留在 JSON 中，同时导出 Markdown 与 CSV。低于计时器或框架开销的结果会明确标记，不会伪装成精确的单次纳秒测量。

性能回归门禁必须显式启用：

```text
dotnet run -c Release -- --benchmarks --baseline previous/benchmark-results.json --max-regression 0.05
```

## 配置

项目默认值可写入 `xfe.runsettings.json`。优先级依次为 CLI、方法/类/程序集特性、配置文件、内置默认值。

```json
{
  "language": "Auto",
  "tests": { "parallel": true, "maxParallelism": 8 },
  "benchmark": { "targetIterationMilliseconds": 500, "maxRelativeError": 0.02 },
  "reports": { "artifactsPath": "XfeTestArtifacts" }
}
```

常用命令包括 `--tests`、`--benchmarks`、`--all`、`--list`、`--filter`、`--category`、`--parallel`、`--no-parallel`、`--fail-fast`、`--explicit`、`--language`、`--artifacts`、`--baseline`、`--max-regression` 和 `--help`。

## 扩展点

通过程序集级 `[UseExtension(typeof(...))]` 可注册 `ITestReporter`、`IBenchmarkExporter`，以及单个 `ITestActivator`。`[MemberData]` 成员既可以返回数据行枚举，也可以返回 `ITestCaseDataSource`；激活器负责本次运行中的测试/Fixture 创建和异步释放。

分析器会拒绝 `async void` 和无效生命周期签名，并为 3.x 旧特性提供迁移诊断与代码修复。代码修复被放在独立程序集，因此命令行分析器/生成器不会依赖 Roslyn Workspaces。

## 从 3.x 迁移

`CTest`、`MTest`、`MRTest`、`SMTest`、`SetUp` 和 `XFECode` 在整个 4.x 中继续保留并标记为过时。请迁移到 `TestFixture`、`Test`、`TestCase`、`Benchmark`、`BeforeEach` 和 `Assert`。旧计时特性会作为基准处理，因此只在使用 `--benchmarks` 时执行；兼容 API 将在 5.0 删除。
