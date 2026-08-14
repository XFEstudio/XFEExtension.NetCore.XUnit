namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 禁止测试类或测试方法与其他测试并行执行。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NonParallelAttribute : Attribute;
