namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 将方法标记为由 XFE 基准引擎测量的性能基准。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BenchmarkAttribute : Attribute
{
    /// <summary>
    /// 获取或设置基准在控制台和报告中的自定义显示名称。
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 获取或设置该方法是否为相同类型、参数组合和作业中的比率基线。
    /// </summary>
    public bool Baseline { get; set; }

    /// <summary>
    /// 获取或设置基准采用的测量策略。
    /// </summary>
    public BenchmarkStrategy Strategy { get; set; } = BenchmarkStrategy.Throughput;
}
