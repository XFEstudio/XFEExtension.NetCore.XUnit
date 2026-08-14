namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 为基准方法提供一组内联调用参数。
/// </summary>
/// <param name="arguments">按基准方法形参顺序提供的参数。</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ArgumentsAttribute(params object?[] arguments) : Attribute
{
    /// <summary>
    /// 获取传递给基准方法的参数。
    /// </summary>
    public object?[] Arguments { get; } = arguments;
}
