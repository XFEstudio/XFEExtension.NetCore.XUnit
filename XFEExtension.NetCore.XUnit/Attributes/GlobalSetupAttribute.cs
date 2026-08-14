namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 将无参方法标记为单个基准工作进程开始测量前调用一次的全局初始化方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GlobalSetupAttribute : Attribute;
