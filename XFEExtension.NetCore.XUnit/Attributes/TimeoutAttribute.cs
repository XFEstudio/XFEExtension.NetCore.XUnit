namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 为测试方法设置硬超时；运行器会在独立工作进程中执行并在超时后回收该进程。
/// </summary>
/// <param name="milliseconds">允许测试执行的最大毫秒数。</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TimeoutAttribute(int milliseconds) : Attribute
{
    /// <summary>
    /// 获取超时毫秒数。
    /// </summary>
    public int Milliseconds { get; } = milliseconds;
}
