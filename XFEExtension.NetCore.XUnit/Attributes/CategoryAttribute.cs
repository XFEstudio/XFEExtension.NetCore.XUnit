namespace XFEExtension.NetCore.XUnit.Attributes;

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
