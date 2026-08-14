using System.Diagnostics;
using System.Reflection;
using XFEExtension.NetCore.XUnit.Reporting;
using XFEExtension.NetCore.XUnit.Runtime;

namespace XFEExtension.NetCore.XUnit.Execution;

internal static class WorkerProcess
{
    public static async Task<TestCaseResult> RunTestAsync(TestDescriptor descriptor, TestRunSettings settings, CancellationToken cancellationToken)
    {
        var resultPath = GetTemporaryPath("test");
        var settingsPath = GetTemporaryPath("settings");
        await File.WriteAllTextAsync(settingsPath, BuiltInReporters.Serialize(settings), cancellationToken).ConfigureAwait(false);
        var start = Stopwatch.GetTimestamp();
        Process? process = null;
        try
        {
            process = Start("--xfe-worker-test", descriptor.Id, resultPath, settingsPath);
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var timeout = descriptor.TimeoutMilliseconds > 0 ? descriptor.TimeoutMilliseconds : settings.DefaultTimeoutMilliseconds;
            var completed = await WaitAsync(process, timeout, cancellationToken).ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(start);
            if (!completed)
            {
                await EnsureStoppedAsync(process).ConfigureAwait(false);
                var timedOutOutput = await stdoutTask.ConfigureAwait(false) + await stderrTask.ConfigureAwait(false);
                return new TestCaseResult(descriptor.Id, descriptor.DisplayName, TestOutcome.TimedOut, TimeSpan.Zero, elapsed, 1,
                    $"Test exceeded the {timeout} ms timeout and its worker process was terminated.", Output: timedOutOutput);
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            if (File.Exists(resultPath))
            {
                var result = BuiltInReporters.Deserialize<TestCaseResult>(await File.ReadAllTextAsync(resultPath, cancellationToken).ConfigureAwait(false));
                if (result is not null)
                    return result with { Output = string.Concat(result.Output, stdout, stderr) };
            }
            return new TestCaseResult(descriptor.Id, descriptor.DisplayName, TestOutcome.Crashed, TimeSpan.Zero, elapsed, 1,
                $"Worker exited with code {process.ExitCode} without producing a result.", Output: stdout + stderr);
        }
        finally
        {
            if (process is not null)
            {
                await EnsureStoppedAsync(process).ConfigureAwait(false);
                process.Dispose();
            }
            TryDelete(resultPath);
            TryDelete(settingsPath);
        }
    }

    public static async Task<BenchmarkSummary> RunBenchmarkAsync(BenchmarkDescriptor descriptor, BenchmarkJob job, CancellationToken cancellationToken)
    {
        var resultPath = GetTemporaryPath("benchmark");
        var settingsPath = GetTemporaryPath("settings");
        await File.WriteAllTextAsync(settingsPath, BuiltInReporters.Serialize(job), cancellationToken).ConfigureAwait(false);
        Process? process = null;
        try
        {
            process = Start("--xfe-worker-benchmark", descriptor.Id, resultPath, settingsPath);
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            if (process.ExitCode == 0 && File.Exists(resultPath))
            {
                var result = BuiltInReporters.Deserialize<BenchmarkSummary>(await File.ReadAllTextAsync(resultPath, cancellationToken).ConfigureAwait(false));
                if (result is not null)
                    return result;
            }
            throw new InvalidOperationException($"Benchmark worker failed with exit code {process.ExitCode}.{Environment.NewLine}{stdout}{stderr}");
        }
        finally
        {
            if (process is not null)
            {
                await EnsureStoppedAsync(process).ConfigureAwait(false);
                process.Dispose();
            }
            TryDelete(resultPath);
            TryDelete(settingsPath);
        }
    }

    private static Process Start(string mode, string id, string resultPath, string settingsPath)
    {
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the current executable path.");
        var info = new ProcessStartInfo(processPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Environment.CurrentDirectory
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(Assembly.GetEntryAssembly()?.Location ?? throw new InvalidOperationException("Cannot determine the entry assembly."));
        info.ArgumentList.Add(mode);
        info.ArgumentList.Add(id);
        info.ArgumentList.Add("--xfe-result");
        info.ArgumentList.Add(resultPath);
        info.ArgumentList.Add("--xfe-settings");
        info.ArgumentList.Add(settingsPath);
        return Process.Start(info) ?? throw new InvalidOperationException("Failed to start the worker process.");
    }

    private static async Task<bool> WaitAsync(Process process, int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (timeoutMilliseconds <= 0)
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        var waitTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
        return await Task.WhenAny(waitTask, timeoutTask).ConfigureAwait(false) == waitTask;
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch
        {
            // The process may have exited between the checks.
        }
    }

    private static async Task EnsureStoppedAsync(Process process)
    {
        try
        {
            Kill(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Process teardown is best effort; the caller will report the original failure.
        }
    }

    private static string GetTemporaryPath(string kind) => Path.Combine(Path.GetTempPath(), $"xfe-{kind}-{Guid.NewGuid():N}.json");

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Temporary files are best-effort cleanup only.
        }
    }
}
