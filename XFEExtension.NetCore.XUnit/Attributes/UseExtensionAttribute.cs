namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 在测试程序集上注册运行时扩展实现。
/// </summary>
/// <param name="extensionType">
/// 实现 <see cref="Runtime.ITestReporter"/>、<see cref="Runtime.IBenchmarkExporter"/>
/// 或 <see cref="Runtime.ITestActivator"/> 的类型。
/// </param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class UseExtensionAttribute(Type extensionType) : Attribute
{
    /// <summary>
    /// 获取要由运行器实例化的扩展类型。
    /// </summary>
    public Type ExtensionType { get; } = extensionType;
}
