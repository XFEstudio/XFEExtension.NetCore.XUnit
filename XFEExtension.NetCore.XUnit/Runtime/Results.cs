namespace XFEExtension.NetCore.XUnit.Runtime;

/// <summary>
/// 表示普通测试用例的最终执行状态。
/// </summary>
public enum TestOutcome
{
    /// <summary>
    /// 测试方法和生命周期均成功完成。
    /// </summary>
    Passed,

    /// <summary>
    /// 测试断言、用户代码或生命周期方法抛出异常。
    /// </summary>
    Failed,

    /// <summary>
    /// 测试因跳过或未选择显式用例而没有执行。
    /// </summary>
    Skipped,

    /// <summary>
    /// 测试超过允许时限，其工作进程已被终止或调用已被标记为超时。
    /// </summary>
    TimedOut,

    /// <summary>
    /// 隔离工作进程异常退出且未生成有效结果。
    /// </summary>
    Crashed
}

/// <summary>
/// 保存一个普通测试用例的结构化执行结果。
/// </summary>
/// <param name="Id">测试用例的稳定唯一标识。</param>
/// <param name="DisplayName">用于控制台和报告的显示名称。</param>
/// <param name="Outcome">测试的最终执行状态。</param>
/// <param name="BodyDuration">仅测试方法主体的执行时间。</param>
/// <param name="TotalDuration">包括构造、生命周期和清理在内的总时间。</param>
/// <param name="Attempts">为获得最终结果实际执行的次数。</param>
/// <param name="Message">失败、跳过、超时或崩溃的说明。</param>
/// <param name="StackTrace">失败异常的堆栈信息。</param>
/// <param name="Output">测试执行期间捕获的标准输出和错误输出。</param>
public sealed record TestCaseResult(
    string Id,
    string DisplayName,
    TestOutcome Outcome,
    TimeSpan BodyDuration,
    TimeSpan TotalDuration,
    int Attempts,
    string? Message = null,
    string? StackTrace = null,
    string? Output = null);

/// <summary>
/// 汇总一次普通测试运行的时间、全部用例结果和状态计数。
/// </summary>
public sealed class TestRunSummary
{
    /// <summary>
    /// 获取本次运行开始的 UTC 时间。
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// 获取本次运行的总墙钟时间。
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// 获取全部测试用例结果。
    /// </summary>
    public IReadOnlyList<TestCaseResult> Results { get; init; } = [];

    /// <summary>
    /// 获取发现并报告的测试用例总数。
    /// </summary>
    public int Total => Results.Count;

    /// <summary>
    /// 获取成功通过的测试用例数。
    /// </summary>
    public int Passed => Results.Count(static result => result.Outcome == TestOutcome.Passed);

    /// <summary>
    /// 获取失败、超时或工作进程崩溃的测试用例数。
    /// </summary>
    public int Failed => Results.Count(static result => result.Outcome is TestOutcome.Failed or TestOutcome.TimedOut or TestOutcome.Crashed);

    /// <summary>
    /// 获取跳过或未选择的显式测试用例数。
    /// </summary>
    public int Skipped => Results.Count(static result => result.Outcome == TestOutcome.Skipped);
}

/// <summary>
/// 表示一次基准实际采样及其批量操作数和异常值状态。
/// </summary>
/// <param name="LaunchIndex">产生样本的独立工作进程启动索引。</param>
/// <param name="IterationIndex">该启动内部从零开始的实际采样索引。</param>
/// <param name="Operations">本轮批量执行的操作数。</param>
/// <param name="NanosecondsPerOperation">扣除适用开销后的每操作纳秒数。</param>
/// <param name="IsOutlier">该样本是否被 Tukey 上侧规则标记为异常值。</param>
public sealed record BenchmarkMeasurement(int LaunchIndex, int IterationIndex, long Operations, double NanosecondsPerOperation, bool IsOutlier = false);

/// <summary>
/// 保存清洗后基准样本的描述统计和置信区间结果。
/// </summary>
public sealed class BenchmarkStatistics
{
    /// <summary>
    /// 获取每操作耗时的算术均值，单位为纳秒。
    /// </summary>
    public double MeanNanoseconds { get; init; }

    /// <summary>
    /// 获取 99.9% 置信区间的半宽，单位为纳秒。
    /// </summary>
    public double ErrorNanoseconds { get; init; }

    /// <summary>
    /// 获取样本标准差，单位为纳秒。
    /// </summary>
    public double StandardDeviationNanoseconds { get; init; }

    /// <summary>
    /// 获取每操作耗时的中位数，单位为纳秒。
    /// </summary>
    public double MedianNanoseconds { get; init; }

    /// <summary>
    /// 获取每操作耗时的第 95 百分位，单位为纳秒。
    /// </summary>
    public double P95Nanoseconds { get; init; }

    /// <summary>
    /// 获取清洗后样本的最小每操作纳秒数。
    /// </summary>
    public double MinNanoseconds { get; init; }

    /// <summary>
    /// 获取清洗后样本的最大每操作纳秒数。
    /// </summary>
    public double MaxNanoseconds { get; init; }

    /// <summary>
    /// 获取根据均值换算的每秒操作数；均值非正时返回 0。
    /// </summary>
    public double OperationsPerSecond => MeanNanoseconds <= 0 ? 0 : 1_000_000_000d / MeanNanoseconds;

    /// <summary>
    /// 获取置信区间半宽与均值的比值；均值为 0 时返回 0。
    /// </summary>
    public double RelativeError => MeanNanoseconds == 0 ? 0 : ErrorNanoseconds / MeanNanoseconds;

    /// <summary>
    /// 获取被标记为上侧异常值的原始样本数。
    /// </summary>
    public int OutlierCount { get; init; }

    /// <summary>
    /// 获取样本数量和相对误差是否达到作业收敛目标。
    /// </summary>
    public bool Converged { get; init; }
}

/// <summary>
/// 保存与计时采样分轮测得的分配量和垃圾回收频率。
/// </summary>
public sealed class GcStatistics
{
    /// <summary>
    /// 获取扣除空负载后每次操作分配的托管字节数。
    /// </summary>
    public double AllocatedBytesPerOperation { get; init; }

    /// <summary>
    /// 获取每一千次操作触发的第 0 代垃圾回收次数。
    /// </summary>
    public double Gen0CollectionsPerThousandOperations { get; init; }

    /// <summary>
    /// 获取每一千次操作触发的第 1 代垃圾回收次数。
    /// </summary>
    public double Gen1CollectionsPerThousandOperations { get; init; }

    /// <summary>
    /// 获取每一千次操作触发的第 2 代垃圾回收次数。
    /// </summary>
    public double Gen2CollectionsPerThousandOperations { get; init; }
}

/// <summary>
/// 描述产生基准结果的操作系统、运行时、处理器、GC 和计时器环境。
/// </summary>
public sealed class EnvironmentSnapshot
{
    /// <summary>
    /// 获取操作系统说明。
    /// </summary>
    public required string OperatingSystem { get; init; }

    /// <summary>
    /// 获取 .NET 运行时说明。
    /// </summary>
    public required string Framework { get; init; }

    /// <summary>
    /// 获取进程体系结构。
    /// </summary>
    public required string Architecture { get; init; }

    /// <summary>
    /// 获取处理器标识或体系结构回退值。
    /// </summary>
    public required string Processor { get; init; }

    /// <summary>
    /// 获取运行环境可见的逻辑处理器数。
    /// </summary>
    public int ProcessorCount { get; init; }

    /// <summary>
    /// 获取是否启用了服务器垃圾回收。
    /// </summary>
    public bool ServerGc { get; init; }

    /// <summary>
    /// 获取 <see cref="System.Diagnostics.Stopwatch"/> 是否使用高分辨率计数器。
    /// </summary>
    public bool StopwatchIsHighResolution { get; init; }

    /// <summary>
    /// 获取高精度计时器每秒计数频率。
    /// </summary>
    public long StopwatchFrequency { get; init; }

    /// <summary>
    /// 获取由关键环境字段计算的 SHA-256 指纹，用于基线门禁环境匹配。
    /// </summary>
    public required string Fingerprint { get; init; }
}

/// <summary>
/// 汇总一个“方法 × 参数 × 作业”的统计、GC、环境、原始样本和警告。
/// </summary>
public sealed class BenchmarkSummary
{
    /// <summary>
    /// 获取基准用例的稳定唯一标识。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 获取控制台和报告使用的基准显示名称。
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// 获取清洗样本计算得到的统计结果。
    /// </summary>
    public required BenchmarkStatistics Statistics { get; init; }

    /// <summary>
    /// 获取独立内存测量轮次得到的分配和 GC 统计。
    /// </summary>
    public required GcStatistics Gc { get; init; }

    /// <summary>
    /// 获取产生这些测量结果的环境快照。
    /// </summary>
    public required EnvironmentSnapshot Environment { get; init; }

    /// <summary>
    /// 获取包含异常值标记的全部原始实际样本。
    /// </summary>
    public IReadOnlyList<BenchmarkMeasurement> Measurements { get; init; } = [];

    /// <summary>
    /// 获取未收敛、高噪声、趋势、偏态或计时器分辨率等可信度警告。
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// 获取或设置相对于同组基线或历史基线的均值比率。
    /// </summary>
    public double? BaselineRatio { get; set; }
}

/// <summary>
/// 汇总一次包含多个基准用例的完整运行。
/// </summary>
public sealed class BenchmarkRunSummary
{
    /// <summary>
    /// 获取本次基准运行开始的 UTC 时间。
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// 获取全部基准工作进程和报告前计算的总墙钟时间。
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// 获取每个基准用例的汇总结果。
    /// </summary>
    public IReadOnlyList<BenchmarkSummary> Benchmarks { get; init; } = [];

    /// <summary>
    /// 获取启用性能门禁后是否发现超过阈值且具有统计显著性的回归。
    /// </summary>
    public bool RegressionDetected { get; init; }
}
