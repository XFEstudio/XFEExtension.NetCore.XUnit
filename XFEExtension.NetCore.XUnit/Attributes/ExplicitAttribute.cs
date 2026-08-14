namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 将测试标记为仅在显式启用时执行。
/// </summary>
/// <param name="reason">要求显式执行的可选原因。</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ExplicitAttribute(string? reason = null) : Attribute
{
    /// <summary>
    /// 获取要求显式执行的原因。
    /// </summary>
    public string? Reason { get; } = reason;
}
