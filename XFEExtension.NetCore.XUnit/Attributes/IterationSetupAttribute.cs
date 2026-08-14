namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 将无参方法标记为每轮实际测量开始前调用的迭代初始化方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IterationSetupAttribute : Attribute;
