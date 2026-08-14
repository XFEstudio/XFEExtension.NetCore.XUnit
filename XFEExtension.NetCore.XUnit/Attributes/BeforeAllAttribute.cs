namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 将无参静态方法标记为测试类全部用例执行前调用一次的生命周期方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BeforeAllAttribute : Attribute;
