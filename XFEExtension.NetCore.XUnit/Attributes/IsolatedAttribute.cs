namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 强制测试类或测试方法在可独立回收的工作进程中执行。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class IsolatedAttribute : Attribute;
