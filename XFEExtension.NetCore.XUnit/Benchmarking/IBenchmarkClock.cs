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
