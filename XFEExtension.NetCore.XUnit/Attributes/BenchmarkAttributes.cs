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

/// <summary>
/// 为基准方法提供一组内联调用参数。
/// </summary>
/// <param name="arguments">按基准方法形参顺序提供的参数。</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ArgumentsAttribute(params object?[] arguments) : Attribute
{
    /// <summary>
    /// 获取传递给基准方法的参数。
    /// </summary>
    public object?[] Arguments { get; } = arguments;
}

/// <summary>
/// 为基准类的字段或属性声明参数值；运行器会为每种参数组合生成独立基准。
/// </summary>
/// <param name="values">依次应用到字段或属性的候选值。</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ParamsAttribute(params object?[] values) : Attribute
{
    /// <summary>
    /// 获取该基准参数的候选值。
    /// </summary>
    public object?[] Values { get; } = values;
}

/// <summary>
/// 将无参方法标记为单个基准工作进程开始测量前调用一次的全局初始化方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GlobalSetupAttribute : Attribute;

/// <summary>
/// 将无参方法标记为单个基准工作进程全部测量完成后调用一次的全局清理方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GlobalCleanupAttribute : Attribute;

/// <summary>
/// 将无参方法标记为每轮实际测量开始前调用的迭代初始化方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IterationSetupAttribute : Attribute;

/// <summary>
/// 将无参方法标记为每轮实际测量结束后调用的迭代清理方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IterationCleanupAttribute : Attribute;

/// <summary>
/// 指定基准引擎如何组织预热、采样和工作进程。
/// </summary>
public enum BenchmarkStrategy
{
    /// <summary>
    /// 通过批量调用、预热、开销扣除和统计采样测量稳定吞吐量。
    /// </summary>
    Throughput,

    /// <summary>
    /// 在多个全新工作进程中各测量一次，用于观察启动和首次执行成本。
    /// </summary>
    ColdStart,

    /// <summary>
    /// 不执行吞吐量校准和预热，以连续单次采样观察较长或随时间变化的工作负载。
    /// </summary>
    Monitoring
}
