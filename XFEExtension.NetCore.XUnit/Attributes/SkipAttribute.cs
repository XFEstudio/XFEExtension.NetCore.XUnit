namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 无条件跳过测试类或测试方法，并在结果中记录原因。
/// </summary>
/// <param name="reason">跳过测试的原因。</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipAttribute(string reason) : Attribute
{
    /// <summary>
    /// 获取跳过测试的原因。
    /// </summary>
    public string Reason { get; } = reason;
}
