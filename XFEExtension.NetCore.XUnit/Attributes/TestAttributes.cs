namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 将类标记为包含 XFE 测试用例的测试夹具。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class TestFixtureAttribute : Attribute;

/// <summary>
/// 将无数据参数的方法标记为普通测试。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute
{
    /// <summary>
    /// 获取或设置测试在列表、控制台和报告中显示的自定义名称。
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// 使用一组内联参数将方法声明为一个测试用例。
/// </summary>
/// <param name="arguments">按测试方法形参顺序提供的参数。</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class TestCaseAttribute(params object?[] arguments) : Attribute
{
    /// <summary>
    /// 获取传递给测试方法的参数。
    /// </summary>
    public object?[] Arguments { get; } = arguments;

    /// <summary>
    /// 获取或设置该数据用例的自定义显示名称。
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// 从静态字段、属性、无参方法或 <see cref="XFEExtension.NetCore.XUnit.ITestCaseDataSource"/> 获取测试数据。
/// </summary>
/// <param name="memberName">提供数据的静态成员名称。</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class MemberDataAttribute(string memberName) : Attribute
{
    /// <summary>
    /// 获取提供测试数据的成员名称。
    /// </summary>
    public string MemberName { get; } = memberName;

    /// <summary>
    /// 获取或设置数据成员所在的类型；为空时使用被标记方法的声明类型。
    /// </summary>
    public Type? MemberType { get; set; }
}

/// <summary>
/// 将无参静态方法标记为测试类全部用例执行前调用一次的生命周期方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BeforeAllAttribute : Attribute;

/// <summary>
/// 将无参静态方法标记为测试类全部用例执行后调用一次的生命周期方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AfterAllAttribute : Attribute;

/// <summary>
/// 将无参方法标记为每个测试用例执行前调用的生命周期方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BeforeEachAttribute : Attribute;

/// <summary>
/// 将无参方法标记为每个测试用例执行后调用的生命周期方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AfterEachAttribute : Attribute;

/// <summary>
/// 为测试类或测试方法附加可用于筛选的分类名称。
/// </summary>
/// <param name="name">分类名称。</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class CategoryAttribute(string name) : Attribute
{
    /// <summary>
    /// 获取分类名称。
    /// </summary>
    public string Name { get; } = name;
}

/// <summary>
/// 为测试类或测试方法附加结构化的名称和值元数据。
/// </summary>
/// <param name="name">特征名称。</param>
/// <param name="value">特征值。</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class TraitAttribute(string name, string value) : Attribute
{
    /// <summary>
    /// 获取特征名称。
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// 获取特征值。
    /// </summary>
    public string Value { get; } = value;
}

/// <summary>
/// 无条件跳过测试类或测试方法，并在结果中记录原因。
/// </summary>
/// <param name="reason">跳过测试的原因。</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipAttribute(string reason) : Attribute
{
    /// <summary>
    /// 获取跳过测试的原因。
    /// </summary>
    public string Reason { get; } = reason;
}

/// <summary>
/// 将测试标记为仅在显式启用时执行。
/// </summary>
/// <param name="reason">要求显式执行的可选原因。</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ExplicitAttribute(string? reason = null) : Attribute
{
    /// <summary>
    /// 获取要求显式执行的原因。
    /// </summary>
    public string? Reason { get; } = reason;
}

/// <summary>
/// 为测试方法设置硬超时；运行器会在独立工作进程中执行并在超时后回收该进程。
/// </summary>
/// <param name="milliseconds">允许测试执行的最大毫秒数。</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TimeoutAttribute(int milliseconds) : Attribute
{
    /// <summary>
    /// 获取超时毫秒数。
    /// </summary>
    public int Milliseconds { get; } = milliseconds;
}

/// <summary>
/// 指定测试失败后允许重新执行的次数。
/// </summary>
/// <param name="count">首次执行之外的最大重试次数。</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RetryAttribute(int count) : Attribute
{
    /// <summary>
    /// 获取最大重试次数。
    /// </summary>
    public int Count { get; } = count;
}

/// <summary>
/// 将测试类加入具名集合，使同一集合中的类共享串行调度和集合级 Fixture 范围。
/// </summary>
/// <param name="name">集合名称。</param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CollectionAttribute(string name) : Attribute
{
    /// <summary>
    /// 获取集合名称。
    /// </summary>
    public string Name { get; } = name;
}

/// <summary>
/// 禁止测试类或测试方法与其他测试并行执行。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NonParallelAttribute : Attribute;

/// <summary>
/// 强制测试类或测试方法在可独立回收的工作进程中执行。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class IsolatedAttribute : Attribute;

/// <summary>
/// 在测试程序集上注册运行时扩展实现。
/// </summary>
/// <param name="extensionType">
/// 实现 <see cref="XFEExtension.NetCore.XUnit.ITestReporter"/>、<see cref="XFEExtension.NetCore.XUnit.IBenchmarkExporter"/>
/// 或 <see cref="XFEExtension.NetCore.XUnit.ITestActivator"/> 的类型。
/// </param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class UseExtensionAttribute(Type extensionType) : Attribute
{
    /// <summary>
    /// 获取要由运行器实例化的扩展类型。
    /// </summary>
    public Type ExtensionType { get; } = extensionType;
}
