using System.Diagnostics;

namespace XFEExtension.NetCore.XUnit.Benchmarking;

/// <summary>
/// 抽象基准引擎使用的单调高精度时钟，便于替换真实时钟和注入确定性测试时钟。
/// </summary>
public interface IBenchmarkClock
{
    /// <summary>
    /// 获取时钟每秒产生的时间戳计数。
    /// </summary>
    long Frequency { get; }

    /// <summary>
    /// 获取当前单调时间戳。
    /// </summary>
    /// <returns>当前时钟计数。</returns>
    long GetTimestamp();

    /// <summary>
    /// 将两个时间戳之间的差值换算为纳秒。
    /// </summary>
    /// <param name="startTimestamp">测量开始时的时间戳。</param>
    /// <param name="endTimestamp">测量结束时的时间戳。</param>
    /// <returns>两个时间戳之间经过的纳秒数。</returns>
    double GetElapsedNanoseconds(long startTimestamp, long endTimestamp);
}

/// <summary>
/// 使用 <see cref="Stopwatch"/> 的单调高精度计数器实现基准时钟。
/// </summary>
public sealed class StopwatchBenchmarkClock : IBenchmarkClock
{
    /// <summary>
    /// 获取当前平台上 <see cref="Stopwatch"/> 每秒的计数频率。
    /// </summary>
    public long Frequency => Stopwatch.Frequency;

    /// <summary>
    /// 获取 <see cref="Stopwatch"/> 的当前时间戳。
    /// </summary>
    /// <returns>当前高精度计数器值。</returns>
    public long GetTimestamp() => Stopwatch.GetTimestamp();

    /// <summary>
    /// 根据平台计数频率将时间戳差值转换为纳秒。
    /// </summary>
    /// <param name="startTimestamp">测量开始时的计数器值。</param>
    /// <param name="endTimestamp">测量结束时的计数器值。</param>
    /// <returns>换算后的经过纳秒数。</returns>
    public double GetElapsedNanoseconds(long startTimestamp, long endTimestamp) =>
        (endTimestamp - startTimestamp) * (1_000_000_000d / Stopwatch.Frequency);
}

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
