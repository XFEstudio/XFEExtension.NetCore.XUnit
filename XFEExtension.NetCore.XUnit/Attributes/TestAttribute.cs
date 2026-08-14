namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 将无数据参数的方法标记为普通测试。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute
{
    /// <summary>
    /// 获取或设置测试在列表、控制台和报告中显示的自定义名称。
    /// </summary>
    public string? Name { get; set; }
}
