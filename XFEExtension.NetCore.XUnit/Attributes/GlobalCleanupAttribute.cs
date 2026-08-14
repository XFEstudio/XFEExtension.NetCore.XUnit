namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 将无参方法标记为单个基准工作进程全部测量完成后调用一次的全局清理方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GlobalCleanupAttribute : Attribute;
