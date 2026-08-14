namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 为基准类的字段或属性声明参数值；运行器会为每种参数组合生成独立基准。
/// </summary>
/// <param name="values">依次应用到字段或属性的候选值。</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ParamsAttribute(params object?[] values) : Attribute
{
    /// <summary>
    /// 获取该基准参数的候选值。
    /// </summary>
    public object?[] Values { get; } = values;
}
