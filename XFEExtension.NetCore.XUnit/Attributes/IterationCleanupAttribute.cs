namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 将无参方法标记为每轮实际测量结束后调用的迭代清理方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IterationCleanupAttribute : Attribute;
