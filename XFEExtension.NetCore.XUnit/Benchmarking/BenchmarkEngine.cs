using XFEExtension.NetCore.XUnit.Attributes;
using XFEExtension.NetCore.XUnit.Runtime;

namespace XFEExtension.NetCore.XUnit.Benchmarking;

/// <summary>
/// 执行单个基准描述符的校准、预热、开销测量、实际采样和内存统计流程。
/// </summary>
/// <param name="clock">可选的单调基准时钟；为空时使用 <see cref="StopwatchBenchmarkClock"/>。</param>
public sealed class BenchmarkEngine(IBenchmarkClock? clock = null)
{
    private readonly IBenchmarkClock _clock = clock ?? new StopwatchBenchmarkClock();

    /// <summary>
    /// 按指定作业执行一次基准启动，并返回原始样本、清洗统计、GC 数据和环境信息。
    /// </summary>
    /// <param name="descriptor">描述调用器、参数、生命周期和测量策略的基准描述符。</param>
    /// <param name="job">控制预热、采样、误差目标和内存测量的作业配置。</param>
    /// <param name="launchIndex">当前启动在多进程聚合结果中的从零开始索引。</param>
    /// <param name="cancellationToken">用于取消校准、预热、采样或生命周期方法的令牌。</param>
    /// <returns>包含本次启动全部测量结果的任务。</returns>
    /// <exception cref="InvalidOperationException">当前处于调试或非优化环境，且作业未允许不安全环境。</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> 已请求取消。</exception>
    public async Task<BenchmarkSummary> RunAsync(BenchmarkDescriptor descriptor, BenchmarkJob job, int launchIndex = 0, CancellationToken cancellationToken = default)
    {
        var warnings = EnvironmentInspector.ValidateBenchmarkEnvironment().ToList();
        if (!job.AllowUnsafeEnvironment && warnings.Any(static warning => warning.StartsWith("DebuggerAttached", StringComparison.Ordinal) || warning.StartsWith("NonOptimizedAssembly", StringComparison.Ordinal)))
            throw new InvalidOperationException("Benchmark refused an unsafe environment: " + string.Join(" ", warnings));
        var instance = descriptor.Factory();
        descriptor.ApplyParameters(instance);
        try
        {
            await InvokeHooks(descriptor.Lifecycle.GlobalSetup, instance).ConfigureAwait(false);
            var operationCount = descriptor.Strategy == BenchmarkStrategy.Throughput
                ? await PilotAsync(descriptor, instance, job, cancellationToken).ConfigureAwait(false)
                : 1L;

            if (descriptor.Strategy == BenchmarkStrategy.Throughput)
                await WarmupAsync(descriptor, instance, operationCount, job, cancellationToken).ConfigureAwait(false);

            var overhead = descriptor.Strategy == BenchmarkStrategy.Throughput
                ? await MeasureOverheadAsync(descriptor.OverheadInvoker, instance, operationCount, cancellationToken).ConfigureAwait(false)
                : 0d;

            var rawMeasurements = new List<BenchmarkMeasurement>();
            BenchmarkStatistics statistics = new();
            bool[] outlierFlags = [];
            var maxIterations = descriptor.Strategy == BenchmarkStrategy.ColdStart
                ? 1
                : descriptor.Strategy == BenchmarkStrategy.Monitoring
                ? Math.Max(job.MinIterationCount, Math.Min(job.MaxIterationCount, 15))
                : job.MaxIterationCount;

            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await InvokeHooks(descriptor.Lifecycle.IterationSetup, instance).ConfigureAwait(false);
                var elapsed = await MeasureIterationAsync(descriptor.Invoker, instance, descriptor.Arguments, operationCount, cancellationToken).ConfigureAwait(false);
                await InvokeHooks(descriptor.Lifecycle.IterationCleanup, instance).ConfigureAwait(false);
                var adjusted = descriptor.Strategy == BenchmarkStrategy.Throughput ? Math.Max(0, elapsed - overhead) : elapsed;
                rawMeasurements.Add(new BenchmarkMeasurement(launchIndex, iteration, operationCount, adjusted));
                (statistics, outlierFlags) = BenchmarkStatisticsCalculator.Calculate(rawMeasurements.Select(static item => item.NanosecondsPerOperation).ToArray(), job.MaxRelativeError);
                if (iteration + 1 >= job.MinIterationCount && statistics.Converged)
                    break;
            }

            var measurements = rawMeasurements.Select((measurement, index) => measurement with
            {
                IsOutlier = index < outlierFlags.Length && outlierFlags[index]
            }).ToArray();

            if (!statistics.Converged)
                warnings.Add($"NotConverged: relative error {statistics.RelativeError:P2} exceeded target {job.MaxRelativeError:P2}.");
            if (statistics.MeanNanoseconds > 0 && statistics.StandardDeviationNanoseconds / statistics.MeanNanoseconds > 0.10)
                warnings.Add("HighNoise: standard deviation exceeds 10% of the mean.");
            var cleanSamples = measurements.Where(static measurement => !measurement.IsOutlier)
                .Select(static measurement => measurement.NanosecondsPerOperation).ToArray();
            if (BenchmarkStatisticsCalculator.HasSignificantTrend(cleanSamples))
                warnings.Add("MeasurementTrend: samples show a significant time-dependent trend.");
            if (statistics.MeanNanoseconds > 0 && Math.Abs(statistics.MeanNanoseconds - statistics.MedianNanoseconds) / statistics.MeanNanoseconds > 0.05)
                warnings.Add("DistributionSkew: mean and median differ by more than 5%.");
            if (statistics.MeanNanoseconds <= 0 || operationCount == 1 && statistics.MeanNanoseconds < 1_000_000_000d / _clock.Frequency)
                warnings.Add("BelowTimerResolution: adjusted workload time is at or below the measurable clock/infrastructure overhead.");

            var gc = job.MeasureMemory
                ? await MeasureGcAsync(descriptor, instance, Math.Clamp(operationCount, 1, 10_000), cancellationToken).ConfigureAwait(false)
                : new GcStatistics();

            return new BenchmarkSummary
            {
                Id = descriptor.Id,
                DisplayName = descriptor.DisplayName,
                Statistics = statistics,
                Gc = gc,
                Measurements = measurements,
                Environment = EnvironmentInspector.Capture(),
                Warnings = warnings
            };
        }
        finally
        {
            await InvokeHooks(descriptor.Lifecycle.GlobalCleanup, instance).ConfigureAwait(false);
            await XfeObjectFactory.DisposeAsync(instance).ConfigureAwait(false);
        }
    }

    private async Task<long> PilotAsync(BenchmarkDescriptor descriptor, object? instance, BenchmarkJob job, CancellationToken cancellationToken)
    {
        var targetNanoseconds = job.TargetIterationMilliseconds * 1_000_000d;
        long operations = 1;
        while (true)
        {
            var elapsed = await MeasureIterationAsync(descriptor.Invoker, instance, descriptor.Arguments, operations, cancellationToken).ConfigureAwait(false) * operations;
            if (elapsed >= targetNanoseconds || operations >= 1L << 30)
                return operations;
            var scale = Math.Clamp((long)Math.Ceiling(targetNanoseconds / Math.Max(1, elapsed)), 2, 16);
            operations = checked(operations * scale);
        }
    }

    private async Task WarmupAsync(BenchmarkDescriptor descriptor, object? instance, long operations, BenchmarkJob job, CancellationToken cancellationToken)
    {
        var samples = new List<double>();
        for (var i = 0; i < job.MaxWarmupCount; i++)
        {
            samples.Add(await MeasureIterationAsync(descriptor.Invoker, instance, descriptor.Arguments, operations, cancellationToken).ConfigureAwait(false));
            if (i + 1 >= job.MinWarmupCount && BenchmarkStatisticsCalculator.IsWarmupStable(samples))
                return;
        }
    }

    private async Task<double> MeasureOverheadAsync(XfeInvoker overheadInvoker, object? instance, long operations, CancellationToken cancellationToken)
    {
        var samples = new double[5];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = await MeasureIterationAsync(overheadInvoker, instance, [], operations, cancellationToken).ConfigureAwait(false);
        Array.Sort(samples);
        return samples[samples.Length / 2];
    }

    private async Task<double> MeasureIterationAsync(XfeInvoker invoker, object? instance, object?[] arguments, long operations, CancellationToken cancellationToken)
    {
        var start = _clock.GetTimestamp();
        for (long operation = 0; operation < operations; operation++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BenchmarkConsumer.Consume(await invoker(instance, arguments).ConfigureAwait(false));
        }
        var end = _clock.GetTimestamp();
        return _clock.GetElapsedNanoseconds(start, end) / operations;
    }

    private async Task<GcStatistics> MeasureGcAsync(BenchmarkDescriptor descriptor, object? instance, long operations, CancellationToken cancellationToken)
    {
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        var emptyBeforeBytes = GC.GetTotalAllocatedBytes(true);
        var emptyBefore0 = GC.CollectionCount(0);
        var emptyBefore1 = GC.CollectionCount(1);
        var emptyBefore2 = GC.CollectionCount(2);
        for (long operation = 0; operation < operations; operation++)
        {
            if ((operation & 1023) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            BenchmarkConsumer.Consume(await descriptor.OverheadInvoker(instance, []).ConfigureAwait(false));
        }
        var emptyAllocated = Math.Max(0, GC.GetTotalAllocatedBytes(true) - emptyBeforeBytes);
        var empty0 = GC.CollectionCount(0) - emptyBefore0;
        var empty1 = GC.CollectionCount(1) - emptyBefore1;
        var empty2 = GC.CollectionCount(2) - emptyBefore2;
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        var beforeBytes = GC.GetTotalAllocatedBytes(true);
        var before0 = GC.CollectionCount(0);
        var before1 = GC.CollectionCount(1);
        var before2 = GC.CollectionCount(2);
        for (long operation = 0; operation < operations; operation++)
        {
            if ((operation & 1023) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            BenchmarkConsumer.Consume(await descriptor.Invoker(instance, descriptor.Arguments).ConfigureAwait(false));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var allocated = Math.Max(0, GC.GetTotalAllocatedBytes(true) - beforeBytes - emptyAllocated);
        return new GcStatistics
        {
            AllocatedBytesPerOperation = (double)allocated / operations,
            Gen0CollectionsPerThousandOperations = Math.Max(0, GC.CollectionCount(0) - before0 - empty0) * 1000d / operations,
            Gen1CollectionsPerThousandOperations = Math.Max(0, GC.CollectionCount(1) - before1 - empty1) * 1000d / operations,
            Gen2CollectionsPerThousandOperations = Math.Max(0, GC.CollectionCount(2) - before2 - empty2) * 1000d / operations
        };
    }

    internal static async ValueTask InvokeHooks(IEnumerable<XfeInvoker> hooks, object? instance)
    {
        foreach (var hook in hooks)
            BenchmarkConsumer.Consume(await hook(instance, []).ConfigureAwait(false));
    }
}
