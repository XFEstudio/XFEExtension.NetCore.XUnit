namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 为 3.x 兼容特性保存构造参数的旧基类；新代码应使用 4.x 测试和基准特性。
/// </summary>
[Obsolete("Legacy attribute base. Use the XUnit 4.0 attributes; this type will be removed in XUnit 5.0.")]
public class XFETestAttributeBase : Attribute
{
    /// <summary>
    /// 获取或设置旧执行器传递给测试类或方法的参数。
    /// </summary>
    public object?[]? Params { get; set; }
}

/// <summary>
/// 兼容 3.x 的测试类标记；新代码应使用 <see cref="TestFixtureAttribute"/>。
/// </summary>
[Obsolete("Use TestFixtureAttribute. Legacy attributes will be removed in XUnit 5.0.")]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class CTestAttribute : XFETestAttributeBase
{
    /// <summary>
    /// 初始化旧测试类特性。
    /// </summary>
    /// <param name="values">传递给测试类构造函数的参数。</param>
    public CTestAttribute(params object?[] values) => Params = values;
}

/// <summary>
/// 兼容 3.x 的具名测试类标记；新代码应使用 <see cref="TestFixtureAttribute"/>。
/// </summary>
[Obsolete("Use TestFixtureAttribute. Legacy attributes will be removed in XUnit 5.0.")]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class CNTestAttribute : CTestAttribute
{
    /// <summary>
    /// 获取或设置旧报告中显示的测试类别名。
    /// </summary>
    public string ClassOtherName { get; set; } = string.Empty;

    /// <summary>
    /// 初始化具名旧测试类特性。
    /// </summary>
    /// <param name="classOtherName">测试类显示名称。</param>
    /// <param name="values">传递给测试类构造函数的参数。</param>
    public CNTestAttribute(string classOtherName, params object?[] values) : base(values) => ClassOtherName = classOtherName;
}

/// <summary>
/// 兼容 3.x 的普通测试方法标记；新代码应使用 <see cref="TestAttribute"/> 或 <see cref="TestCaseAttribute"/>。
/// </summary>
[Obsolete("Use TestAttribute or TestCaseAttribute. Legacy attributes will be removed in XUnit 5.0.")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class MTestAttribute : XFETestAttributeBase
{
    /// <summary>
    /// 初始化旧测试方法特性。
    /// </summary>
    /// <param name="values">传递给测试方法的参数。</param>
    public MTestAttribute(params object?[] values) => Params = values;
}

/// <summary>
/// 兼容 3.x 的具名测试方法标记；新代码应使用 <see cref="TestCaseAttribute.Name"/>。
/// </summary>
[Obsolete("Use TestCaseAttribute.Name. Legacy attributes will be removed in XUnit 5.0.")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class MNTestAttribute : MTestAttribute
{
    /// <summary>
    /// 获取或设置旧报告中显示的测试方法别名。
    /// </summary>
    public string? MethodOtherName { get; set; }

    /// <summary>
    /// 使用显示名称和调用参数初始化旧测试方法特性。
    /// </summary>
    /// <param name="methodOtherName">测试方法显示名称。</param>
    /// <param name="values">传递给测试方法的参数。</param>
    public MNTestAttribute(string methodOtherName, params object?[] values) : base(values) => MethodOtherName = methodOtherName;

    /// <summary>
    /// 初始化不指定名称和参数的旧测试方法特性。
    /// </summary>
    public MNTestAttribute() { }
}

/// <summary>
/// 兼容 3.x 的返回值比较测试；兼容执行器会把最后一个构造参数作为期望返回值。
/// </summary>
[Obsolete("Use TestCaseAttribute and Assert.Equal. Legacy attributes will be removed in XUnit 5.0.")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class MRTestAttribute : MTestAttribute
{
    /// <summary>
    /// 获取或设置测试方法的期望返回值。
    /// </summary>
    public object? ReturnValue { get; set; }

    /// <summary>
    /// 使用方法参数和位于最后一项的期望返回值初始化旧返回值测试。
    /// </summary>
    /// <param name="valuesAndResult">方法参数，最后一项为期望返回值。</param>
    public MRTestAttribute(params object?[] valuesAndResult)
    {
        ReturnValue = valuesAndResult.Length == 0 ? null : valuesAndResult[^1];
        Params = valuesAndResult.Length == 0 ? [] : valuesAndResult[..^1];
    }
    /// <summary>
    /// 初始化不指定方法参数和期望返回值的旧返回值测试。
    /// </summary>
    public MRTestAttribute() { }
}

/// <summary>
/// 兼容 3.x 的具名返回值比较测试。
/// </summary>
[Obsolete("Use TestCaseAttribute.Name and Assert.Equal. Legacy attributes will be removed in XUnit 5.0.")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class MNRTestAttribute : MRTestAttribute
{
    /// <summary>
    /// 获取或设置旧报告中显示的测试方法别名。
    /// </summary>
    public string? MethodOtherName { get; set; }

    /// <summary>
    /// 使用显示名称、方法参数和期望返回值初始化旧测试。
    /// </summary>
    /// <param name="methodOtherName">测试方法显示名称。</param>
    /// <param name="valuesAndResult">方法参数，最后一项为期望返回值。</param>
    public MNRTestAttribute(string methodOtherName, params object?[] valuesAndResult) : base(valuesAndResult) => MethodOtherName = methodOtherName;

    /// <summary>
    /// 初始化不指定名称、参数和期望返回值的旧测试。
    /// </summary>
    public MNRTestAttribute() { }
}

/// <summary>
/// 兼容 3.x 的单方法计时特性；4.x 在默认测试模式中单次执行并展示其全部控制台输出。
/// </summary>
[Obsolete("Use TestAttribute or TestCaseAttribute. Legacy attributes will be removed in XUnit 5.0.")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class SMTestAttribute : XFETestAttributeBase
{
    /// <summary>
    /// 初始化旧单次执行测试特性。
    /// </summary>
    /// <param name="values">传递给基准方法的参数。</param>
    public SMTestAttribute(params object?[] values) => Params = values;
}

/// <summary>
/// 兼容 3.x 的具名单方法计时特性。
/// </summary>
[Obsolete("Use TestAttribute or TestCaseAttribute. Legacy attributes will be removed in XUnit 5.0.")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class SMNTestAttribute : SMTestAttribute
{
    /// <summary>
    /// 获取或设置旧输出中显示的计时器名称。
    /// </summary>
    public string TimerName { get; set; } = string.Empty;

    /// <summary>
    /// 使用计时器名称和调用参数初始化旧单次执行测试。
    /// </summary>
    /// <param name="timerName">计时器显示名称。</param>
    /// <param name="values">传递给基准方法的参数。</param>
    public SMNTestAttribute(string timerName, params object?[] values) : base(values) => TimerName = timerName;
}

/// <summary>
/// 兼容 3.x 的带返回值单方法计时特性。
/// </summary>
[Obsolete("Use TestCaseAttribute and Assert.Equal. Legacy attributes will be removed in XUnit 5.0.")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class SMRTestAttribute : SMTestAttribute
{
    /// <summary>
    /// 获取或设置旧执行器使用的期望返回值。
    /// </summary>
    public object? ReturnValue { get; set; }

    /// <summary>
    /// 使用方法参数和位于最后一项的期望返回值初始化旧单次执行测试。
    /// </summary>
    /// <param name="valuesAndResult">方法参数，最后一项为期望返回值。</param>
    public SMRTestAttribute(params object?[] valuesAndResult)
    {
        ReturnValue = valuesAndResult.Length == 0 ? null : valuesAndResult[^1];
        Params = valuesAndResult.Length == 0 ? [] : valuesAndResult[..^1];
    }
    /// <summary>
    /// 初始化不指定参数和期望返回值的旧基准。
    /// </summary>
    public SMRTestAttribute() { }
}

/// <summary>
/// 兼容 3.x 的具名带返回值单方法计时特性。
/// </summary>
[Obsolete("Use TestCaseAttribute and Assert.Equal. Legacy attributes will be removed in XUnit 5.0.")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class SMNRTestAttribute : SMRTestAttribute
{
    /// <summary>
    /// 获取或设置旧输出中显示的计时器名称。
    /// </summary>
    public string? TimerName { get; set; }

    /// <summary>
    /// 使用计时器名称、方法参数和期望返回值初始化旧单次执行测试。
    /// </summary>
    /// <param name="timerName">计时器显示名称。</param>
    /// <param name="valuesAndResult">方法参数，最后一项为期望返回值。</param>
    public SMNRTestAttribute(string timerName, params object?[] valuesAndResult) : base(valuesAndResult) => TimerName = timerName;

    /// <summary>
    /// 初始化不指定名称、参数和期望返回值的旧基准。
    /// </summary>
    public SMNRTestAttribute() { }
}

/// <summary>
/// 兼容 3.x 的测试初始化方法特性；新代码应使用 <see cref="BeforeEachAttribute"/>。
/// </summary>
[Obsolete("Use BeforeEachAttribute. Legacy attributes will be removed in XUnit 5.0.")]
[AttributeUsage(AttributeTargets.Method)]
public sealed class SetUpAttribute : XFETestAttributeBase
{
    /// <summary>
    /// 初始化旧测试初始化方法特性。
    /// </summary>
    /// <param name="values">旧执行器传递给初始化方法的参数。</param>
    public SetUpAttribute(params object?[] values) => Params = values;
}

/// <summary>
/// 保存 3.x 控制台测试输出配色；4.x 结构化报告不会使用这些颜色。
/// </summary>
/// <param name="mainColor">主标题颜色。</param>
/// <param name="classColor">测试类名称颜色。</param>
/// <param name="classBorderColor">测试类边框颜色。</param>
/// <param name="methodColor">测试方法名称颜色。</param>
/// <param name="methodBorderColor">测试方法边框颜色。</param>
/// <param name="successColor">成功结果颜色。</param>
/// <param name="failColor">失败结果颜色。</param>
/// <param name="timeColor">时间信息颜色。</param>
/// <param name="counterColor">计数信息颜色。</param>
[Obsolete("Console themes are not part of XUnit 4.0 structured reporting and will be removed in XUnit 5.0.")]
[AttributeUsage(AttributeTargets.Class)]
public sealed class TestThemeAttribute(
    ConsoleColor mainColor = ConsoleColor.Blue,
    ConsoleColor classColor = ConsoleColor.Green,
    ConsoleColor classBorderColor = ConsoleColor.DarkGreen,
    ConsoleColor methodColor = ConsoleColor.Yellow,
    ConsoleColor methodBorderColor = ConsoleColor.DarkYellow,
    ConsoleColor successColor = ConsoleColor.Green,
    ConsoleColor failColor = ConsoleColor.Red,
    ConsoleColor timeColor = ConsoleColor.Cyan,
    ConsoleColor counterColor = ConsoleColor.Gray) : Attribute
{
    /// <summary>
    /// 获取或设置主标题颜色。
    /// </summary>
    public ConsoleColor MainColor { get; set; } = mainColor;

    /// <summary>
    /// 获取或设置测试类名称颜色。
    /// </summary>
    public ConsoleColor ClassColor { get; set; } = classColor;

    /// <summary>
    /// 获取或设置测试类边框颜色。
    /// </summary>
    public ConsoleColor ClassBorderColor { get; set; } = classBorderColor;

    /// <summary>
    /// 获取或设置测试方法名称颜色。
    /// </summary>
    public ConsoleColor MethodColor { get; set; } = methodColor;

    /// <summary>
    /// 获取或设置测试方法边框颜色。
    /// </summary>
    public ConsoleColor MethodBorderColor { get; set; } = methodBorderColor;

    /// <summary>
    /// 获取或设置成功结果颜色。
    /// </summary>
    public ConsoleColor SuccessColor { get; set; } = successColor;

    /// <summary>
    /// 获取或设置失败结果颜色。
    /// </summary>
    public ConsoleColor FailColor { get; set; } = failColor;

    /// <summary>
    /// 获取或设置时间信息颜色。
    /// </summary>
    public ConsoleColor TimeColor { get; set; } = timeColor;

    /// <summary>
    /// 获取或设置计数信息颜色。
    /// </summary>
    public ConsoleColor CounterColor { get; set; } = counterColor;
}
