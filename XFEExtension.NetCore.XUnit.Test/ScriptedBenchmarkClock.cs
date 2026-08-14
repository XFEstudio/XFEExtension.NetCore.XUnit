using XFEExtension.NetCore.XUnit.Benchmarking;

namespace XFEExtension.NetCore.XUnit.Test;

internal sealed class ScriptedBenchmarkClock(IReadOnlyList<long> elapsedTicks) : IBenchmarkClock
{
    private int _timestampCall;
    private long _timestamp;

    public long Frequency => 10_000_000;

    public long GetTimestamp()
    {
        if ((_timestampCall++ & 1) == 0)
            return _timestamp;
        var durationIndex = _timestampCall / 2 - 1;
        if (durationIndex >= elapsedTicks.Count)
            throw new InvalidOperationException("The benchmark requested more clock samples than the test supplied.");
        _timestamp += elapsedTicks[durationIndex];
        return _timestamp;
    }

    public double GetElapsedNanoseconds(long startTimestamp, long endTimestamp) =>
        (endTimestamp - startTimestamp) * (1_000_000_000d / Frequency);
}
