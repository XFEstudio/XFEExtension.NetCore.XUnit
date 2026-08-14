namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 将无参方法标记为每个测试用例执行前调用的生命周期方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BeforeEachAttribute : Attribute;
