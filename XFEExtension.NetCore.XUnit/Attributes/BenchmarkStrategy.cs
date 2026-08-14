namespace XFEExtension.NetCore.XUnit.Attributes;

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
