namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 从静态字段、属性、无参方法或 <see cref="Runtime.ITestCaseDataSource"/> 获取测试数据。
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
