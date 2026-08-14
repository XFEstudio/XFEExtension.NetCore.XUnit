namespace XFEExtension.NetCore.XUnit.Attributes;

/// <summary>
/// 将类标记为包含 XFE 测试用例的测试夹具。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class TestFixtureAttribute : Attribute;
