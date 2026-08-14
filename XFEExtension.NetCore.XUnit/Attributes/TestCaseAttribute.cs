namespace XFEExtension.NetCore.XUnit.Attributes;

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
