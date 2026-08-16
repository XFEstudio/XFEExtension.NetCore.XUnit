using System.Diagnostics;
using System.Runtime.ExceptionServices;
using XFEExtension.NetCore.XUnit.Assertions;
using XFEExtension.NetCore.XUnit.Benchmarking;
using XFEExtension.NetCore.XUnit.Runtime;

namespace XFEExtension.NetCore.XUnit.Execution;

internal sealed class TestExecutor
{
    public async Task<IReadOnlyList<TestCaseResult>> RunAsync(IReadOnlyList<TestDescriptor> tests, TestRunSettings settings, bool ignoreIsolation, CancellationToken cancellationToken)
    {
        AsyncLocalConsoleCapture.Install();
        var results = new List<TestCaseResult>();
        var resultLock = new object();
        var groups = tests.GroupBy(static test => test.Collection ?? test.TypeName).ToArray();
        var gate = new SemaphoreSlim(settings.Parallel ? Math.Max(1, settings.MaxParallelism) : 1);
        var parallelGroups = groups.Where(static group => !group.Any(static test => test.NonParallel)).ToArray();
        var serialGroups = groups.Except(parallelGroups).ToArray();
        var tasks = parallelGroups.Select(async group =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var groupResults = await RunGroupAsync(group.ToArray(), settings, ignoreIsolation, cancellationToken).ConfigureAwait(false);
                lock (resultLock)
                    results.AddRange(groupResults);
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        foreach (var group in serialGroups)
        {
            var groupResults = await RunGroupAsync(group.ToArray(), settings, ignoreIsolation, cancellationToken).ConfigureAwait(false);
            results.AddRange(groupResults);
        }
        return results.OrderBy(static result => result.DisplayName, StringComparer.Ordinal).ToArray();
    }

    private static async Task<IReadOnlyList<TestCaseResult>> RunGroupAsync(TestDescriptor[] tests, TestRunSettings settings, bool ignoreIsolation, CancellationToken cancellationToken)
    {
        var results = new List<TestCaseResult>();
        using var fixtureScope = XfeObjectFactory.BeginFixtureScope(out var fixtures);
        foreach (var classGroup in tests.GroupBy(static test => test.TypeName))
        {
            var first = classGroup.First();
            object? legacyInstance = null;
            Exception? beforeAllFailure = null;
            try
            {
                if (first.IsLegacy)
                {
                    legacyInstance = first.Factory();
                    await BenchmarkEngine.InvokeHooks(first.Lifecycle.BeforeEach, legacyInstance).ConfigureAwait(false);
                }
                await BenchmarkEngine.InvokeHooks(first.Lifecycle.BeforeAll, legacyInstance).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                beforeAllFailure = exception;
            }

            foreach (var test in classGroup)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (beforeAllFailure is not null)
                {
                    results.Add(FailedFromException(test, beforeAllFailure, TestOutcome.Failed, 0, TimeSpan.Zero, TimeSpan.Zero, null));
                    continue;
                }
                if (test.SkipReason is not null || test.Explicit && !settings.IncludeExplicit)
                {
                    results.Add(WithDescriptor(test, new TestCaseResult(test.Id, test.DisplayName, TestOutcome.Skipped, TimeSpan.Zero, TimeSpan.Zero, 0,
                        test.SkipReason ?? "Explicit test was not selected.")));
                    continue;
                }

                results.Add(await RunCaseAsync(test, settings, ignoreIsolation, legacyInstance, cancellationToken).ConfigureAwait(false));
                if (settings.FailFast && results[^1].Outcome is TestOutcome.Failed or TestOutcome.TimedOut or TestOutcome.Crashed)
                    break;
            }

            try
            {
                await BenchmarkEngine.InvokeHooks(first.Lifecycle.AfterAll, legacyInstance).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                results.Add(FailedFromException(first, exception, TestOutcome.Failed, 0, TimeSpan.Zero, TimeSpan.Zero, null));
            }

            await DisposeInstanceAsync(legacyInstance).ConfigureAwait(false);
        }
        foreach (var fixture in fixtures.Reverse())
            await XfeObjectFactory.DisposeAsync(fixture).ConfigureAwait(false);
        return results;
    }

    private static async Task<TestCaseResult> RunCaseAsync(TestDescriptor test, TestRunSettings settings, bool ignoreIsolation, object? sharedInstance, CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, test.RetryCount + 1);
        TestCaseResult? lastResult = null;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var totalWatch = Stopwatch.StartNew();
            var bodyDuration = TimeSpan.Zero;
            var afterEachAttempted = false;
            object? instance = sharedInstance;
            string? output = null;
            using var capture = AsyncLocalConsoleCapture.Begin(out var getOutput);
            try
            {
                instance ??= test.Factory();
                if (!test.IsLegacy)
                    await BenchmarkEngine.InvokeHooks(test.Lifecycle.BeforeEach, instance).ConfigureAwait(false);
                var bodyWatch = Stopwatch.StartNew();
                object? result;
                var timeout = test.TimeoutMilliseconds > 0 ? test.TimeoutMilliseconds : settings.DefaultTimeoutMilliseconds;
                if (timeout > 0 && ignoreIsolation)
                {
                    using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var invocation = test.Invoker(instance, test.Arguments).AsTask();
                    var completed = await Task.WhenAny(invocation, Task.Delay(timeout, timeoutSource.Token)).ConfigureAwait(false);
                    if (completed != invocation)
                        throw new TimeoutException($"Test exceeded the {timeout} ms timeout.");
                    timeoutSource.Cancel();
                    result = await invocation.ConfigureAwait(false);
                }
                else
                {
                    result = await test.Invoker(instance, test.Arguments).ConfigureAwait(false);
                }
                bodyWatch.Stop();
                bodyDuration = bodyWatch.Elapsed;
                if (test.HasExpectedResult && !Equals(test.ExpectedResult, result))
                    throw new XFEAssertionException($"Expected return value {test.ExpectedResult ?? "<null>"}, but found {result ?? "<null>"}.");
                if (!test.IsLegacy)
                {
                    afterEachAttempted = true;
                    await BenchmarkEngine.InvokeHooks(test.Lifecycle.AfterEach, instance).ConfigureAwait(false);
                }
                totalWatch.Stop();
                output = getOutput();
                lastResult = WithDescriptor(test, new TestCaseResult(test.Id, test.DisplayName, TestOutcome.Passed, bodyDuration, totalWatch.Elapsed, attempt, Output: output));
                return lastResult;
            }
            catch (Exception exception)
            {
                output = getOutput();
                try
                {
                    if (!test.IsLegacy && !afterEachAttempted)
                        await BenchmarkEngine.InvokeHooks(test.Lifecycle.AfterEach, instance).ConfigureAwait(false);
                }
                catch (Exception cleanupException)
                {
                    exception = new AggregateException(exception, cleanupException);
                }
                totalWatch.Stop();
                var outcome = exception is TimeoutException ? TestOutcome.TimedOut : TestOutcome.Failed;
                lastResult = FailedFromException(test, exception, outcome, attempt, bodyDuration, totalWatch.Elapsed, output);
            }
            finally
            {
                if (sharedInstance is null)
                    await XfeObjectFactory.DisposeAsync(instance).ConfigureAwait(false);
            }
        }
        return lastResult!;
    }

    private static TestCaseResult FailedFromException(TestDescriptor test, Exception exception, TestOutcome outcome, int attempts, TimeSpan body, TimeSpan total, string? output)
    {
        var actual = exception is AggregateException aggregateException ? aggregateException.Flatten() : exception;
        return WithDescriptor(test, new TestCaseResult(test.Id, test.DisplayName, outcome, body, total, attempts, actual.Message, actual.StackTrace, output));
    }

    private static TestCaseResult WithDescriptor(TestDescriptor test, TestCaseResult result) => result with
    {
        IsLegacySingleRun = test.IsLegacySingleRun,
        TypeName = test.TypeName,
        MethodName = test.MethodName
    };

    private static ValueTask DisposeInstanceAsync(object? instance) => XfeObjectFactory.DisposeAsync(instance);
}
