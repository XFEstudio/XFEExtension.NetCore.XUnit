using System.Diagnostics;

namespace XFEExtension.NetCore.XUnit;

/// <summary>
/// 提供 3.x 源代码迁移所需的旧测试基类和辅助方法；新代码应直接使用 <see cref="XfeRunner"/>、
/// <see cref="Assert"/> 和 <see cref="Attributes.BenchmarkAttribute"/>。
/// </summary>
[Obsolete("Use XfeRunner, Assert and BenchmarkAttribute. XFECode will be removed in XUnit 5.0.")]
public abstract class XFECode
{
    /// <summary>
    /// 使用当前进程命令行参数运行生成注册表中的测试，并把结果写入 <see cref="Environment.ExitCode"/>。
    /// </summary>
    /// <returns>表示整个运行完成的任务。</returns>
    [Obsolete("Use generated XfeRunner.RunAsync entry points.")]
    public static async Task RunTest() => Environment.ExitCode = await XfeRunner.RunAsync(Environment.GetCommandLineArgs().Skip(1).ToArray()).ConfigureAwait(false);

    /// <summary>
    /// 对委托执行一次墙钟计时；该兼容方法不会校准、预热或统计采样，不能替代正式基准。
    /// </summary>
    /// <param name="action">要执行一次的同步委托；为空时仅测量空调用路径。</param>
    /// <param name="autoOutPut">是否把格式化时间写入控制台。</param>
    /// <param name="timerName">控制台输出中使用的计时器名称。</param>
    /// <returns>该次调用测得的墙钟时间。</returns>
    [Obsolete("One-shot timing is not a benchmark. Use BenchmarkAttribute and --benchmarks.")]
    public static TimeSpan CTime(Action? action, bool autoOutPut = true, string timerName = "unnamed")
    {
        var elapsed = Measure(action);
        if (autoOutPut)
            Console.WriteLine($"Name: {timerName}\tElapsed: {Format(elapsed)} (one-shot measurement; not a statistical benchmark)");
        return elapsed;
    }

    /// <summary>
    /// 完整等待异步委托并执行一次墙钟计时；该兼容方法不能替代正式基准。
    /// </summary>
    /// <param name="action">要执行并等待一次的异步委托；为空时只测量计时器路径。</param>
    /// <param name="autoOutPut">是否把格式化时间写入控制台。</param>
    /// <param name="timerName">控制台输出中使用的计时器名称。</param>
    /// <returns>结果为该次异步调用墙钟时间的任务。</returns>
    [Obsolete("One-shot timing is not a benchmark. Use BenchmarkAttribute and --benchmarks.")]
    public static async Task<TimeSpan> CTimeAsync(Func<Task>? action, bool autoOutPut = true, string timerName = "unnamed")
    {
        var stopwatch = Stopwatch.StartNew();
        if (action is not null)
            await action().ConfigureAwait(false);
        stopwatch.Stop();
        if (autoOutPut)
            Console.WriteLine($"Name: {timerName}\tElapsed: {Format(stopwatch.Elapsed)} (one-shot measurement; not a statistical benchmark)");
        return stopwatch.Elapsed;
    }

    /// <summary>
    /// 兼容旧代码并验证条件为真。
    /// </summary>
    /// <param name="condition">要验证的条件。</param>
    /// <param name="message">失败时使用的可选消息。</param>
    /// <returns>断言成功时始终返回 <see langword="true"/>。</returns>
    /// <exception cref="XfeAssertionException"><paramref name="condition"/> 为 <see langword="false"/>。</exception>
    [Obsolete("Use Assert.True.")]
    public static bool Assert(bool condition, string? message = null)
    {
        global::XFEExtension.NetCore.XUnit.Assert.True(condition, message);
        return true;
    }

    /// <summary>
    /// 兼容旧代码并验证条件为假。
    /// </summary>
    /// <param name="condition">要验证的条件。</param>
    /// <param name="message">失败时使用的可选消息。</param>
    /// <returns>断言成功时始终返回 <see langword="true"/>。</returns>
    /// <exception cref="XfeAssertionException"><paramref name="condition"/> 为 <see langword="true"/>。</exception>
    [Obsolete("Use Assert.False.")]
    public static bool AssertF(bool condition, string? message = null)
    {
        global::XFEExtension.NetCore.XUnit.Assert.False(condition, message);
        return true;
    }

    /// <summary>
    /// 兼容旧代码并验证期望值与实际值相等。
    /// </summary>
    /// <typeparam name="T">参与比较的值类型。</typeparam>
    /// <param name="expected">期望值。</param>
    /// <param name="actual">实际值。</param>
    /// <param name="message">失败时使用的可选消息。</param>
    /// <returns>断言成功时始终返回 <see langword="true"/>。</returns>
    /// <exception cref="XfeAssertionException">两个值不相等。</exception>
    [Obsolete("Use Assert.Equal.")]
    public static bool AssertE<T>(T expected, T actual, string? message = null)
    {
        global::XFEExtension.NetCore.XUnit.Assert.Equal(expected, actual, message);
        return true;
    }

    /// <summary>
    /// 在线程池中并行启动指定次数的同步操作，并可选择等待全部任务。
    /// </summary>
    /// <param name="action">每个任务执行的同步操作。</param>
    /// <param name="count">要启动的任务数。</param>
    /// <param name="autoWaitAll">是否在返回前等待全部任务完成。</param>
    /// <returns>包含全部已启动任务的列表。</returns>
    protected static async Task<List<Task>> Circle(Action action, int count, bool autoWaitAll = false)
    {
        var tasks = Enumerable.Range(0, count).Select(_ => Task.Run(action)).ToList();
        if (autoWaitAll)
            await Task.WhenAll(tasks).ConfigureAwait(false);
        return tasks;
    }

    /// <summary>
    /// 按顺序在线程池中执行指定次数的同步操作。
    /// </summary>
    /// <param name="action">每次执行的同步操作。</param>
    /// <param name="count">顺序执行次数。</param>
    /// <returns>表示全部操作完成的任务。</returns>
    protected static async Task CircleOrderly(Action action, int count)
    {
        for (var i = 0; i < count; i++)
            await Task.Run(action).ConfigureAwait(false);
    }

    /// <summary>
    /// 可选显示默认提示，然后等待用户按下任意键。
    /// </summary>
    /// <param name="showText">是否显示默认提示文本。</param>
    protected static void Pause(bool showText = true)
    {
        if (showText)
            Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }

    /// <summary>
    /// 显示指定提示并等待用户按下任意键。
    /// </summary>
    /// <param name="showText">等待按键前显示的文本。</param>
    protected static void Pause(string showText)
    {
        Console.WriteLine(showText);
        Console.ReadKey();
    }

    /// <summary>
    /// 等待用户按下指定控制台按键。
    /// </summary>
    /// <param name="consoleKey">用于继续执行的按键。</param>
    protected static void Pause(ConsoleKey consoleKey)
    {
        Console.WriteLine($"Press {consoleKey} to continue...");
        while (Console.ReadKey().Key != consoleKey) { }
    }

    /// <summary>
    /// 显示指定提示并等待用户按下指定控制台按键。
    /// </summary>
    /// <param name="consoleKey">用于继续执行的按键。</param>
    /// <param name="showText">等待按键前显示的文本。</param>
    protected static void Pause(ConsoleKey consoleKey, string showText)
    {
        Console.WriteLine(showText);
        while (Console.ReadKey().Key != consoleKey) { }
    }

    private static TimeSpan Measure(Action? action)
    {
        var stopwatch = Stopwatch.StartNew();
        action?.Invoke();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static string Format(TimeSpan elapsed) => elapsed.TotalNanoseconds switch
    {
        >= 1_000_000_000 => $"{elapsed.TotalSeconds:F3} s",
        >= 1_000_000 => $"{elapsed.TotalMilliseconds:F3} ms",
        >= 1_000 => $"{elapsed.TotalMicroseconds:F3} us",
        _ => $"{elapsed.TotalNanoseconds:F3} ns"
    };
}
