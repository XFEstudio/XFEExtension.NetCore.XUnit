namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 指定测试失败后允许重新执行的次数。
/// </summary>
/// <param name="count">首次执行之外的最大重试次数。</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RetryAttribute(int count) : Attribute
{
    /// <summary>
    /// 获取最大重试次数。
    /// </summary>
    public int Count { get; } = count;
}
