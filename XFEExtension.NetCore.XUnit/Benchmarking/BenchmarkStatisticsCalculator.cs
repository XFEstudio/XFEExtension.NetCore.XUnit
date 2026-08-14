using XFEExtension.NetCore.XUnit.Runtime;

namespace XFEExtension.NetCore.XUnit.Benchmarking;

/// <summary>
/// 提供基准样本的异常值识别、描述统计、置信区间、预热稳定性和趋势计算。
/// </summary>
public static class BenchmarkStatisticsCalculator
{
    /// <summary>
    /// 使用 Tukey 上侧围栏标记异常值，并根据清洗后的样本计算 99.9% 置信区间统计。
    /// </summary>
    /// <param name="samples">按采集顺序排列的每操作纳秒样本。</param>
    /// <param name="maxRelativeError">判定收敛所允许的最大相对置信区间半宽。</param>
    /// <returns>计算后的统计模型，以及与输入样本索引一一对应的异常值标记。</returns>
    public static (BenchmarkStatistics Statistics, bool[] Outliers) Calculate(IReadOnlyList<double> samples, double maxRelativeError)
    {
        if (samples.Count == 0)
            return (new BenchmarkStatistics(), []);

        var sorted = samples.Order().ToArray();
        var q1 = Percentile(sorted, 0.25);
        var q3 = Percentile(sorted, 0.75);
        var upperFence = q3 + 1.5 * (q3 - q1);
        var outliers = samples.Select(value => value > upperFence).ToArray();
        var filtered = samples.Where((_, index) => !outliers[index]).ToArray();
        if (filtered.Length < 2)
            filtered = samples.ToArray();

        Array.Sort(filtered);
        var mean = filtered.Average();
        var variance = filtered.Length <= 1
            ? 0
            : filtered.Sum(value => Math.Pow(value - mean, 2)) / (filtered.Length - 1);
        var standardDeviation = Math.Sqrt(variance);
        var criticalValue = CriticalValue999(filtered.Length - 1);
        var error = filtered.Length <= 1 ? 0 : criticalValue * standardDeviation / Math.Sqrt(filtered.Length);

        return (new BenchmarkStatistics
        {
            MeanNanoseconds = mean,
            ErrorNanoseconds = error,
            StandardDeviationNanoseconds = standardDeviation,
            MedianNanoseconds = Percentile(filtered, 0.50),
            P95Nanoseconds = Percentile(filtered, 0.95),
            MinNanoseconds = filtered[0],
            MaxNanoseconds = filtered[^1],
            OutlierCount = outliers.Count(static value => value),
            Converged = filtered.Length >= 2 && (error == 0 || mean > 0 && error / mean <= maxRelativeError)
        }, outliers);
    }

    /// <summary>
    /// 比较最近两个三样本窗口的中位数，判断预热是否达到 1% 以内的稳定窗口。
    /// </summary>
    /// <param name="samples">按执行顺序排列的预热样本。</param>
    /// <returns>至少存在六个样本且最近两个窗口稳定时为 <see langword="true"/>。</returns>
    public static bool IsWarmupStable(IReadOnlyList<double> samples)
    {
        if (samples.Count < 6)
            return false;
        var previous = Median(samples.Skip(samples.Count - 6).Take(3));
        var current = Median(samples.Skip(samples.Count - 3));
        if (previous == 0)
            return current == 0;
        return Math.Abs(current - previous) / Math.Abs(previous) <= 0.01;
    }

    /// <summary>
    /// 通过线性相关性和首尾相对变化检测样本中的明显时间趋势。
    /// </summary>
    /// <param name="samples">按采集时间顺序排列的清洗样本。</param>
    /// <returns>相关系数绝对值至少为 0.70 且拟合变化至少为均值的 5% 时为 <see langword="true"/>。</returns>
    public static bool HasSignificantTrend(IReadOnlyList<double> samples)
    {
        if (samples.Count < 8)
            return false;
        var meanX = (samples.Count - 1) / 2d;
        var meanY = samples.Average();
        if (meanY == 0)
            return false;
        double covariance = 0;
        double varianceX = 0;
        double varianceY = 0;
        for (var index = 0; index < samples.Count; index++)
        {
            var deltaX = index - meanX;
            var deltaY = samples[index] - meanY;
            covariance += deltaX * deltaY;
            varianceX += deltaX * deltaX;
            varianceY += deltaY * deltaY;
        }
        if (varianceX == 0 || varianceY == 0)
            return false;
        var correlation = covariance / Math.Sqrt(varianceX * varianceY);
        var slope = covariance / varianceX;
        var relativeChange = Math.Abs(slope * (samples.Count - 1) / meanY);
        return Math.Abs(correlation) >= 0.70 && relativeChange >= 0.05;
    }

    /// <summary>
    /// 对已经按升序排列的样本执行线性插值百分位数计算。
    /// </summary>
    /// <param name="sortedSamples">按升序排列的样本。</param>
    /// <param name="percentile">介于 0 与 1 之间的目标百分位。</param>
    /// <returns>插值得到的百分位数；空样本返回 0。</returns>
    public static double Percentile(IReadOnlyList<double> sortedSamples, double percentile)
    {
        if (sortedSamples.Count == 0)
            return 0;
        if (sortedSamples.Count == 1)
            return sortedSamples[0];
        var position = (sortedSamples.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
            return sortedSamples[lower];
        return sortedSamples[lower] + (sortedSamples[upper] - sortedSamples[lower]) * (position - lower);
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        return Percentile(sorted, 0.5);
    }

    private static double CriticalValue999(int degreesOfFreedom) => degreesOfFreedom switch
    {
        <= 1 => 636.62,
        <= 2 => 31.60,
        <= 3 => 12.92,
        <= 4 => 8.61,
        <= 5 => 6.87,
        <= 7 => 5.41,
        <= 10 => 4.59,
        <= 14 => 4.14,
        <= 20 => 3.85,
        <= 30 => 3.65,
        <= 60 => 3.46,
        <= 120 => 3.37,
        _ => 3.291
    };
}
