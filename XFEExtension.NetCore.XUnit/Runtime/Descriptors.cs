using System.Text.Json.Serialization;
using XFEExtension.NetCore.XUnit.Attributes;

namespace XFEExtension.NetCore.XUnit.Runtime;

/// <summary>
/// 表示生成器创建的强类型异步调用适配器。
/// </summary>
/// <param name="instance">实例方法的目标对象；静态方法为 <see langword="null"/>。</param>
/// <param name="arguments">按方法形参顺序排列的调用参数。</param>
/// <returns>表示方法完成的值任务；结果为装箱后的返回值，<see langword="void"/> 方法返回 <see langword="null"/>。</returns>
public delegate ValueTask<object?> XfeInvoker(object? instance, object?[] arguments);

/// <summary>
/// 保存测试和基准各阶段的强类型生命周期调用器。
/// </summary>
public sealed class XfeLifecycleHooks
{
    /// <summary>
    /// 获取测试类全部用例开始前执行一次的调用器。
    /// </summary>
    public IReadOnlyList<XfeInvoker> BeforeAll { get; init; } = [];

    /// <summary>
    /// 获取测试类全部用例结束后执行一次的调用器。
    /// </summary>
    public IReadOnlyList<XfeInvoker> AfterAll { get; init; } = [];

    /// <summary>
    /// 获取每个普通测试用例开始前执行的调用器。
    /// </summary>
    public IReadOnlyList<XfeInvoker> BeforeEach { get; init; } = [];

    /// <summary>
    /// 获取每个普通测试用例结束后执行的调用器。
    /// </summary>
    public IReadOnlyList<XfeInvoker> AfterEach { get; init; } = [];

    /// <summary>
    /// 获取单个基准工作进程开始测量前执行一次的调用器。
    /// </summary>
    public IReadOnlyList<XfeInvoker> GlobalSetup { get; init; } = [];

    /// <summary>
    /// 获取单个基准工作进程完成全部测量后执行一次的调用器。
    /// </summary>
    public IReadOnlyList<XfeInvoker> GlobalCleanup { get; init; } = [];

    /// <summary>
    /// 获取每轮基准实际测量前执行的调用器。
    /// </summary>
    public IReadOnlyList<XfeInvoker> IterationSetup { get; init; } = [];

    /// <summary>
    /// 获取每轮基准实际测量后执行的调用器。
    /// </summary>
    public IReadOnlyList<XfeInvoker> IterationCleanup { get; init; } = [];
}

/// <summary>
/// 描述一个已经展开参数、可由运行器直接执行的普通测试用例。
/// </summary>
public sealed class TestDescriptor
{
    /// <summary>
    /// 获取在生成注册表和工作进程通信中唯一标识该用例的字符串。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 获取控制台、筛选和报告使用的测试显示名称。
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// 获取测试方法声明类型的完全限定名称。
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// 获取测试方法名称。
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// 获取传递给测试调用器的参数。
    /// </summary>
    public object?[] Arguments { get; init; } = [];

    /// <summary>
    /// 获取从测试类和方法合并得到的分类名称。
    /// </summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>
    /// 获取从测试类和方法合并得到的结构化特征。
    /// </summary>
    public IReadOnlyDictionary<string, string> Traits { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// 获取跳过原因；为空表示测试未被无条件跳过。
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// 获取该用例是否仅在启用显式测试时运行。
    /// </summary>
    public bool Explicit { get; init; }

    /// <summary>
    /// 获取该用例是否禁止与其他测试并行执行。
    /// </summary>
    public bool NonParallel { get; init; }

    /// <summary>
    /// 获取该用例是否必须在独立工作进程中执行。
    /// </summary>
    public bool Isolated { get; init; }

    /// <summary>
    /// 获取该用例是否来自 3.x 兼容特性。
    /// </summary>
    public bool IsLegacy { get; init; }

    /// <summary>
    /// 获取该用例是否来自 3.x 的 <see cref="Attributes.SMTestAttribute"/> 单次执行特性。
    /// </summary>
    public bool IsLegacySingleRun { get; init; }

    /// <summary>
    /// 获取测试集合名称；为空时按声明类型分组。
    /// </summary>
    public string? Collection { get; init; }

    /// <summary>
    /// 获取硬超时毫秒数；0 表示使用全局默认值或不超时。
    /// </summary>
    public int TimeoutMilliseconds { get; init; }

    /// <summary>
    /// 获取首次失败后允许重新执行的次数。
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// 获取兼容执行器是否需要比较方法返回值。
    /// </summary>
    public bool HasExpectedResult { get; init; }

    /// <summary>
    /// 获取兼容执行器期望的方法返回值。
    /// </summary>
    public object? ExpectedResult { get; init; }

    /// <summary>
    /// 获取创建测试类实例的工厂；该成员不会序列化到结果文件。
    /// </summary>
    [JsonIgnore] public required Func<object?> Factory { get; init; }

    /// <summary>
    /// 获取直接调用测试方法的强类型适配器；该成员不会序列化到结果文件。
    /// </summary>
    [JsonIgnore] public required XfeInvoker Invoker { get; init; }

    /// <summary>
    /// 获取测试类的生命周期调用器集合。
    /// </summary>
    [JsonIgnore] public XfeLifecycleHooks Lifecycle { get; init; } = new();
}

/// <summary>
/// 描述一个已经展开方法参数和 <see cref="Attributes.ParamsAttribute"/> 组合的基准用例。
/// </summary>
public sealed class BenchmarkDescriptor
{
    /// <summary>
    /// 获取在生成注册表和工作进程通信中唯一标识该基准的字符串。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 获取控制台、筛选和报告使用的基准显示名称。
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// 获取基准方法声明类型的完全限定名称。
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// 获取基准方法名称。
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// 获取传递给基准调用器的方法参数。
    /// </summary>
    public object?[] Arguments { get; init; } = [];

    /// <summary>
    /// 获取从基准类和方法合并得到的分类名称。
    /// </summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>
    /// 获取该基准是否为相同参数组合的比率基线。
    /// </summary>
    public bool Baseline { get; init; }

    /// <summary>
    /// 获取该基准是否来自 3.x 单方法计时兼容特性。
    /// </summary>
    public bool IsLegacy { get; init; }

    /// <summary>
    /// 获取用于匹配相同 <see cref="Attributes.ParamsAttribute"/> 组合基线的稳定键。
    /// </summary>
    public string ParameterKey { get; init; } = string.Empty;

    /// <summary>
    /// 获取该基准采用的测量策略。
    /// </summary>
    public BenchmarkStrategy Strategy { get; init; } = BenchmarkStrategy.Throughput;

    /// <summary>
    /// 获取创建基准类实例的工厂。
    /// </summary>
    [JsonIgnore] public required Func<object?> Factory { get; init; }

    /// <summary>
    /// 获取直接调用基准方法的强类型适配器。
    /// </summary>
    [JsonIgnore] public required XfeInvoker Invoker { get; init; }

    /// <summary>
    /// 获取具有相同返回形状但不执行用户工作负载的开销调用器。
    /// </summary>
    [JsonIgnore] public XfeInvoker OverheadInvoker { get; init; } = static (_, _) => new ValueTask<object?>((object?)null);

    /// <summary>
    /// 获取把当前 <see cref="Attributes.ParamsAttribute"/> 组合应用到基准实例的委托。
    /// </summary>
    [JsonIgnore] public Action<object?> ApplyParameters { get; init; } = static _ => { };

    /// <summary>
    /// 获取基准类的全局和迭代生命周期调用器。
    /// </summary>
    [JsonIgnore] public XfeLifecycleHooks Lifecycle { get; init; } = new();
}

/// <summary>
/// 保存生成器发现的普通测试和基准描述符。
/// </summary>
public sealed class XfeRegistry
{
    private readonly List<TestDescriptor> _tests = [];
    private readonly List<BenchmarkDescriptor> _benchmarks = [];

    /// <summary>
    /// 获取注册的普通测试用例只读列表。
    /// </summary>
    public IReadOnlyList<TestDescriptor> Tests => _tests;

    /// <summary>
    /// 获取注册的基准用例只读列表。
    /// </summary>
    public IReadOnlyList<BenchmarkDescriptor> Benchmarks => _benchmarks;

    /// <summary>
    /// 向注册表添加一个普通测试用例。
    /// </summary>
    /// <param name="descriptor">要注册的测试描述符。</param>
    public void AddTest(TestDescriptor descriptor) => _tests.Add(descriptor);

    /// <summary>
    /// 向注册表添加一个基准用例。
    /// </summary>
    /// <param name="descriptor">要注册的基准描述符。</param>
    public void AddBenchmark(BenchmarkDescriptor descriptor) => _benchmarks.Add(descriptor);
}

/// <summary>
/// 连接增量生成器输出与运行时的进程内注册表入口。
/// </summary>
public static class XfeGeneratedRegistry
{
    private static Func<XfeRegistry>? s_factory;

    /// <summary>
    /// 设置用于创建当前程序集测试注册表的生成工厂。
    /// </summary>
    /// <param name="factory">生成器发出的注册表工厂。</param>
    public static void SetFactory(Func<XfeRegistry> factory) => s_factory = factory;

    /// <summary>
    /// 创建当前程序集的测试注册表；没有生成工厂时返回空注册表。
    /// </summary>
    /// <returns>新创建的测试和基准注册表。</returns>
    public static XfeRegistry Create() => s_factory?.Invoke() ?? new XfeRegistry();
}

/// <summary>
/// 为生成代码提供测试类、基准类和 Fixture 的构造及释放支持。
/// </summary>
public static class XfeObjectFactory
{
    private static readonly AsyncLocal<Dictionary<Type, object>?> s_fixtures = new();
    private static readonly AsyncLocal<ITestActivator?> s_activator = new();

    internal static IDisposable UseActivator(ITestActivator? activator)
    {
        var previous = s_activator.Value;
        s_activator.Value = activator;
        return new ActivatorScope(previous);
    }

    internal static IDisposable BeginFixtureScope(out IReadOnlyCollection<object> fixtures)
    {
        var previous = s_fixtures.Value;
        var current = new Dictionary<Type, object>();
        s_fixtures.Value = current;
        fixtures = current.Values;
        return new FixtureScope(previous);
    }

    /// <summary>
    /// 创建指定类型，优先使用已注册的 <see cref="ITestActivator"/>，然后匹配构造参数或注入 Fixture。
    /// </summary>
    /// <param name="type">要创建的测试、基准或 Fixture 类型。</param>
    /// <param name="arguments">显式传递给构造函数的参数。</param>
    /// <returns>创建的实例；静态类返回 <see langword="null"/>。</returns>
    /// <exception cref="MissingMethodException">没有构造函数能够接收给定参数，且无法使用 Fixture 构造。</exception>
    public static object? Create(Type type, object?[] arguments)
    {
        if (type.IsAbstract && type.IsSealed)
            return null;
        if (arguments.Length == 0 && s_activator.Value?.CreateInstance(type) is { } activated)
            return activated;
        var constructors = type.GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            if (parameters.Length != arguments.Length)
                continue;
            var compatible = true;
            for (var index = 0; index < parameters.Length; index++)
            {
                var value = arguments[index];
                if (value is null ? parameters[index].ParameterType.IsValueType && Nullable.GetUnderlyingType(parameters[index].ParameterType) is null : !parameters[index].ParameterType.IsInstanceOfType(value))
                {
                    compatible = false;
                    break;
                }
            }
            if (compatible)
                return constructor.Invoke(arguments);
        }
        if (arguments.Length == 0)
        {
            var fixtureConstructor = constructors.OrderByDescending(static constructor => constructor.GetParameters().Length).FirstOrDefault();
            if (fixtureConstructor is not null)
            {
                var fixtureArguments = fixtureConstructor.GetParameters().Select(parameter => GetOrCreateFixture(parameter.ParameterType)).ToArray();
                return fixtureConstructor.Invoke(fixtureArguments);
            }
        }
        throw new MissingMethodException($"No constructor on {type.FullName} accepts {arguments.Length} supplied argument(s).");
    }

    /// <summary>
    /// 使用已注册激活器或标准释放接口异步释放实例。
    /// </summary>
    /// <param name="instance">要释放的对象；静态测试可传入 <see langword="null"/>。</param>
    /// <returns>表示释放完成的值任务。</returns>
    public static async ValueTask DisposeAsync(object? instance)
    {
        if (s_activator.Value is { } activator)
        {
            await activator.DisposeAsync(instance).ConfigureAwait(false);
            return;
        }
        if (instance is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        else if (instance is IDisposable disposable)
            disposable.Dispose();
    }

    private static object GetOrCreateFixture(Type type)
    {
        var fixtures = s_fixtures.Value ?? throw new InvalidOperationException("Fixture construction is only available inside an active test collection.");
        if (fixtures.TryGetValue(type, out var fixture))
            return fixture;
        fixture = s_activator.Value?.CreateInstance(type)
            ?? Activator.CreateInstance(type, true)
            ?? throw new InvalidOperationException($"Could not construct fixture {type.FullName}.");
        fixtures.Add(type, fixture);
        return fixture;
    }

    private sealed class FixtureScope(Dictionary<Type, object>? previous) : IDisposable
    {
        public void Dispose() => s_fixtures.Value = previous;
    }

    private sealed class ActivatorScope(ITestActivator? previous) : IDisposable
    {
        public void Dispose() => s_activator.Value = previous;
    }
}

/// <summary>
/// 为生成代码解析 <see cref="Attributes.MemberDataAttribute"/> 指定的静态数据成员。
/// </summary>
public static class XfeMemberData
{
    /// <summary>
    /// 读取静态字段、属性或无参方法，并把数据源规范化为参数数组序列。
    /// </summary>
    /// <param name="sourceType">声明数据成员的类型。</param>
    /// <param name="memberName">静态数据成员名称。</param>
    /// <returns>测试参数行序列。</returns>
    /// <exception cref="MissingMemberException">找不到指定的静态成员。</exception>
    /// <exception cref="InvalidOperationException">成员值既不是 <see cref="ITestCaseDataSource"/> 也不是可枚举数据。</exception>
    public static IEnumerable<object?[]> Get(Type sourceType, string memberName)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        object? value = sourceType.GetProperty(memberName, flags)?.GetValue(null)
            ?? sourceType.GetField(memberName, flags)?.GetValue(null)
            ?? sourceType.GetMethod(memberName, flags, null, Type.EmptyTypes, null)?.Invoke(null, null)
            ?? throw new MissingMemberException(sourceType.FullName, memberName);
        if (value is ITestCaseDataSource dataSource)
        {
            foreach (var row in dataSource.GetData())
                yield return row;
            yield break;
        }
        if (value is not System.Collections.IEnumerable rows)
            throw new InvalidOperationException($"Member data source {sourceType.FullName}.{memberName} must implement IEnumerable.");
        foreach (var row in rows)
        {
            if (row is object?[] array)
                yield return array;
            else if (row is System.Runtime.CompilerServices.ITuple tuple)
            {
                var values = new object?[tuple.Length];
                for (var index = 0; index < tuple.Length; index++)
                    values[index] = tuple[index];
                yield return values;
            }
            else
                yield return [row];
        }
    }
}

/// <summary>
/// 为生成代码把 <see cref="Attributes.ParamsAttribute"/> 的值应用到基准实例成员。
/// </summary>
public static class XfeParameterBinder
{
    /// <summary>
    /// 设置基准实例上的字段或属性值。
    /// </summary>
    /// <param name="instance">包含目标成员的基准实例。</param>
    /// <param name="memberName">要设置的字段或属性名称。</param>
    /// <param name="value">要应用的参数值。</param>
    /// <exception cref="InvalidOperationException"><paramref name="instance"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="MissingMemberException">实例类型上不存在指定字段或属性。</exception>
    public static void Set(object? instance, string memberName, object? value)
    {
        if (instance is null)
            throw new InvalidOperationException($"Cannot set benchmark parameter {memberName} on a static fixture.");
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var type = instance.GetType();
        if (type.GetProperty(memberName, flags) is { } property)
        {
            property.SetValue(instance, value);
            return;
        }
        if (type.GetField(memberName, flags) is { } field)
        {
            field.SetValue(instance, value);
            return;
        }
        throw new MissingMemberException(type.FullName, memberName);
    }
}
