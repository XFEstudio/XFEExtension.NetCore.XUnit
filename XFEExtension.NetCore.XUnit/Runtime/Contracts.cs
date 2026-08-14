namespace XFEExtension.NetCore.XUnit;

/// <summary>
/// 定义普通测试运行完成后的自定义报告扩展。
/// </summary>
public interface ITestReporter
{
    /// <summary>
    /// 将测试运行摘要写入自定义目标。
    /// </summary>
    /// <param name="summary">包含全部测试用例结果的运行摘要。</param>
    /// <param name="artifactsPath">运行器配置的报告产物目录。</param>
    /// <param name="cancellationToken">用于取消报告写入的令牌。</param>
    /// <returns>表示异步报告操作的值任务。</returns>
    ValueTask ReportAsync(TestRunSummary summary, string artifactsPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// 定义基准运行完成后的自定义结果导出扩展。
/// </summary>
public interface IBenchmarkExporter
{
    /// <summary>
    /// 将基准运行摘要导出到自定义格式或目标。
    /// </summary>
    /// <param name="summary">包含统计数据和原始样本的基准运行摘要。</param>
    /// <param name="artifactsPath">运行器配置的报告产物目录。</param>
    /// <param name="cancellationToken">用于取消导出的令牌。</param>
    /// <returns>表示异步导出操作的值任务。</returns>
    ValueTask ExportAsync(BenchmarkRunSummary summary, string artifactsPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// 定义可由 <see cref="Attributes.MemberDataAttribute"/> 成员返回的自定义测试数据源。
/// </summary>
public interface ITestCaseDataSource
{
    /// <summary>
    /// 获取测试方法的参数行。
    /// </summary>
    /// <returns>参数数组序列；每个数组表示一个独立测试用例。</returns>
    IEnumerable<object?[]> GetData();
}

/// <summary>
/// 定义测试类、Fixture 和基准类的自定义创建与释放策略。
/// </summary>
public interface ITestActivator
{
    /// <summary>
    /// 尝试创建指定类型的实例。
    /// </summary>
    /// <param name="type">运行器需要创建的类型。</param>
    /// <returns>创建的实例；返回 <see langword="null"/> 时运行器使用内置构造逻辑。</returns>
    object? CreateInstance(Type type);

    /// <summary>
    /// 释放运行器使用过的实例。
    /// </summary>
    /// <param name="instance">要释放的实例；静态测试可能传入 <see langword="null"/>。</param>
    /// <returns>表示异步释放操作的值任务。</returns>
    ValueTask DisposeAsync(object? instance);
}

/// <summary>
/// 声明测试类需要一个在其执行范围内共享的类 Fixture。
/// </summary>
/// <typeparam name="TFixture">要创建并注入测试类构造函数的 Fixture 类型。</typeparam>
public interface IClassFixture<TFixture> where TFixture : class;

/// <summary>
/// 声明测试集合需要一个在集合执行范围内共享的 Fixture。
/// </summary>
/// <typeparam name="TFixture">要在测试集合内共享的 Fixture 类型。</typeparam>
public interface ICollectionFixture<TFixture> where TFixture : class;

/// <summary>
/// 使用无参构造函数创建对象，并按照异步优先顺序释放对象的默认激活器。
/// </summary>
public sealed class DefaultTestActivator : ITestActivator
{
    /// <summary>
    /// 使用公共无参构造函数创建指定类型。
    /// </summary>
    /// <param name="type">要创建的类型。</param>
    /// <returns>创建的实例。</returns>
    /// <exception cref="MissingMethodException">类型没有可用的公共无参构造函数。</exception>
    public object? CreateInstance(Type type) => Activator.CreateInstance(type);

    /// <summary>
    /// 优先调用 <see cref="IAsyncDisposable.DisposeAsync"/>，否则调用 <see cref="IDisposable.Dispose"/>。
    /// </summary>
    /// <param name="instance">要释放的实例。</param>
    /// <returns>表示释放操作的值任务。</returns>
    public async ValueTask DisposeAsync(object? instance)
    {
        if (instance is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        else if (instance is IDisposable disposable)
            disposable.Dispose();
    }
}
