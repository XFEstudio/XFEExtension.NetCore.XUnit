using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace XFEExtension.NetCore.XUnit;

internal static class EnvironmentInspector
{
    public static EnvironmentSnapshot Capture()
    {
        var processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? RuntimeInformation.ProcessArchitecture.ToString();
        var source = string.Join('|', RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture, processor, Environment.ProcessorCount, GCSettings.IsServerGC,
            Stopwatch.Frequency);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        return new EnvironmentSnapshot
        {
            OperatingSystem = RuntimeInformation.OSDescription,
            Framework = RuntimeInformation.FrameworkDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Processor = processor,
            ProcessorCount = Environment.ProcessorCount,
            ServerGc = GCSettings.IsServerGC,
            StopwatchIsHighResolution = Stopwatch.IsHighResolution,
            StopwatchFrequency = Stopwatch.Frequency,
            Fingerprint = fingerprint
        };
    }

    public static IReadOnlyList<string> ValidateBenchmarkEnvironment()
    {
        var warnings = new List<string>();
        if (Debugger.IsAttached)
            warnings.Add("DebuggerAttached: attached debuggers make benchmark measurements unreliable.");
        var entry = Assembly.GetEntryAssembly();
        if (entry?.GetCustomAttribute<DebuggableAttribute>() is { IsJITOptimizerDisabled: true })
            warnings.Add("NonOptimizedAssembly: run benchmarks from a Release build.");
        if (!Stopwatch.IsHighResolution)
            warnings.Add("LowResolutionClock: Stopwatch is not using a high-resolution performance counter.");
        return warnings;
    }
}
