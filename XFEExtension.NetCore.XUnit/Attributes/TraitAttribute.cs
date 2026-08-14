namespace XFEExtension.NetCore.XUnit.Attributes;

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
