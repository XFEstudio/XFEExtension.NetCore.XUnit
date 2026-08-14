namespace XFEExtension.NetCore.XUnit.Attributes;

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
