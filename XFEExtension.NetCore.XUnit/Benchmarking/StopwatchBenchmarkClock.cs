using System.Diagnostics;

namespace XFEExtension.NetCore.XUnit.Benchmarking;

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
