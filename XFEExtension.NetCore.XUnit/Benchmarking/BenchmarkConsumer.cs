namespace XFEExtension.NetCore.XUnit.Benchmarking;

/// <summary>
/// 接收基准返回值，阻止 JIT 将可观察结果未被使用的工作负载消除。
/// </summary>
public static class BenchmarkConsumer
{
    private static volatile object? s_value;

    /// <summary>
    /// 消费一次基准调用的返回值并建立可观察写入。
    /// </summary>
    /// <param name="value">要消费的返回值；无返回值方法可传入 <see langword="null"/>。</param>
    public static void Consume(object? value) => s_value = value;
}
