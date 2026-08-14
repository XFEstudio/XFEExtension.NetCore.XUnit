namespace XFEExtension.NetCore.XUnit;

/// <summary>
/// 表示从配置文件加载并可由命令行覆盖的完整 XFE 运行设置。
/// </summary>
public sealed class XfeRunSettings
{
    /// <summary>
    /// 获取普通测试的调度和行为设置。
    /// </summary>
    public TestRunSettings Tests { get; init; } = new();

    /// <summary>
    /// 获取基准测量作业设置。
    /// </summary>
    public BenchmarkJob Benchmark { get; init; } = new();

    /// <summary>
    /// 获取内置报告输出设置。
    /// </summary>
    public ReportSettings Reports { get; init; } = new();
}

/// <summary>
/// 控制普通测试的并行度、失败策略、显式用例和默认超时。
/// </summary>
public sealed class TestRunSettings
{
    /// <summary>
    /// 获取或设置是否允许不同测试集合并行执行。
    /// </summary>
    public bool Parallel { get; set; } = true;

    /// <summary>
    /// 获取或设置同时执行的最大测试集合数。
    /// </summary>
    public int MaxParallelism { get; set; } = Math.Max(1, Environment.ProcessorCount);

    /// <summary>
    /// 获取或设置是否在首个失败、超时或崩溃结果后停止调度后续同组用例。
    /// </summary>
    public bool FailFast { get; set; }

    /// <summary>
    /// 获取或设置是否执行带有 <see cref="Attributes.ExplicitAttribute"/> 的测试。
    /// </summary>
    public bool IncludeExplicit { get; set; }

    /// <summary>
    /// 获取或设置未声明 <see cref="Attributes.TimeoutAttribute"/> 时使用的默认超时毫秒数；0 表示不超时。
    /// </summary>
    public int DefaultTimeoutMilliseconds { get; set; }

    /// <summary>
    /// 获取或设置用于稳定随机测试顺序的种子；为空时保持发现顺序。
    /// </summary>
    public int? Seed { get; set; }
}

/// <summary>
/// 定义自适应基准的目标迭代时间、预热、采样、误差和内存测量参数。
/// </summary>
public sealed class BenchmarkJob
{
    /// <summary>
    /// 获取或设置 Pilot 阶段希望每轮批量调用达到的目标毫秒数。
    /// </summary>
    public int TargetIterationMilliseconds { get; set; } = 500;

    /// <summary>
    /// 获取或设置在允许稳定性提前停止前必须执行的最少预热轮数。
    /// </summary>
    public int MinWarmupCount { get; set; } = 6;

    /// <summary>
    /// 获取或设置无法达到稳定窗口时执行的最大预热轮数。
    /// </summary>
    public int MaxWarmupCount { get; set; } = 50;

    /// <summary>
    /// 获取或设置允许置信区间收敛后停止前必须保留的最少实际样本数。
    /// </summary>
    public int MinIterationCount { get; set; } = 15;

    /// <summary>
    /// 获取或设置单次启动最多采集的实际样本数。
    /// </summary>
    public int MaxIterationCount { get; set; } = 100;

    /// <summary>
    /// 获取或设置置信区间半宽相对于均值的最大收敛目标，例如 0.02 表示 2%。
    /// </summary>
    public double MaxRelativeError { get; set; } = 0.02;

    /// <summary>
    /// 获取或设置吞吐量基准未收敛时允许启动的最大独立工作进程数。
    /// </summary>
    public int MaxLaunchCount { get; set; } = 3;

    /// <summary>
    /// 获取或设置是否在计时轮之外测量每操作分配量和 GC 次数。
    /// </summary>
    public bool MeasureMemory { get; set; } = true;

    /// <summary>
    /// 获取或设置是否允许在调试器附加或程序集未优化等不可信环境中继续测量。
    /// </summary>
    public bool AllowUnsafeEnvironment { get; set; }

    /// <summary>
    /// 创建用于开发和 CI 冒烟的短作业；该作业速度更快但统计置信度低于默认作业。
    /// </summary>
    /// <returns>目标 25ms、2 至 4 轮预热和 4 至 8 个样本的基准作业。</returns>
    public static BenchmarkJob Quick() => new()
    {
        TargetIterationMilliseconds = 25,
        MinWarmupCount = 2,
        MaxWarmupCount = 4,
        MinIterationCount = 4,
        MaxIterationCount = 8,
        MaxRelativeError = 0.10,
        MaxLaunchCount = 1
    };
}

/// <summary>
/// 控制内置 JSON、JUnit XML、Markdown 和 CSV 报告的输出位置及启用状态。
/// </summary>
public sealed class ReportSettings
{
    /// <summary>
    /// 获取或设置报告产物目录；相对路径基于运行器工作目录解析。
    /// </summary>
    public string ArtifactsPath { get; set; } = "XfeTestArtifacts";

    /// <summary>
    /// 获取或设置是否写入包含结构化结果和原始基准样本的 JSON 报告。
    /// </summary>
    public bool Json { get; set; } = true;

    /// <summary>
    /// 获取或设置是否为普通测试写入 JUnit XML 报告。
    /// </summary>
    public bool JUnit { get; set; } = true;

    /// <summary>
    /// 获取或设置是否为基准写入 Markdown 汇总报告。
    /// </summary>
    public bool Markdown { get; set; } = true;

    /// <summary>
    /// 获取或设置是否为基准写入 CSV 汇总报告。
    /// </summary>
    public bool Csv { get; set; } = true;
}
