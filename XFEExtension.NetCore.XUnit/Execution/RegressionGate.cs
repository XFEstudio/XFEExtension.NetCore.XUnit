namespace XFEExtension.NetCore.XUnit;

internal static class RegressionGate
{
    public static bool Apply(IReadOnlyList<BenchmarkSummary> current, BenchmarkRunSummary baseline, double threshold, bool allowEnvironmentMismatch, List<string> errors)
    {
        var regression = false;
        foreach (var benchmark in current)
        {
            var previous = baseline.Benchmarks.FirstOrDefault(item => string.Equals(item.Id, benchmark.Id, StringComparison.Ordinal));
            if (previous is null || previous.Statistics.MeanNanoseconds <= 0)
                continue;
            if (!allowEnvironmentMismatch && !string.Equals(previous.Environment.Fingerprint, benchmark.Environment.Fingerprint, StringComparison.Ordinal))
            {
                errors.Add($"Cannot gate {benchmark.DisplayName}: baseline environment fingerprint differs.");
                continue;
            }
            benchmark.BaselineRatio = benchmark.Statistics.MeanNanoseconds / previous.Statistics.MeanNanoseconds;
            var relativeRegression = benchmark.BaselineRatio.Value - 1;
            if (relativeRegression > threshold && IsSignificant(benchmark, previous))
                regression = true;
        }
        return regression;
    }

    private static bool IsSignificant(BenchmarkSummary current, BenchmarkSummary previous)
    {
        var currentValues = current.Measurements.Where(static value => !value.IsOutlier).Select(static value => value.NanosecondsPerOperation).ToArray();
        var previousValues = previous.Measurements.Where(static value => !value.IsOutlier).Select(static value => value.NanosecondsPerOperation).ToArray();
        if (currentValues.Length < 2 || previousValues.Length < 2)
            return false;
        var currentVariance = Variance(currentValues);
        var previousVariance = Variance(previousValues);
        var standardError = Math.Sqrt(currentVariance / currentValues.Length + previousVariance / previousValues.Length);
        return standardError == 0
            ? currentValues.Average() > previousValues.Average()
            : (currentValues.Average() - previousValues.Average()) / standardError >= 2.576;
    }

    private static double Variance(double[] values)
    {
        var mean = values.Average();
        return values.Sum(value => Math.Pow(value - mean, 2)) / (values.Length - 1);
    }
}
