namespace XFEExtension.NetCore.XUnit.Assertions;

/// <summary>
/// 表示 XFE 强类型断言未满足时产生的测试失败。
/// </summary>
/// <param name="message">描述断言期望值与实际值差异的消息。</param>
public sealed class XFEAssertionException(string message) : Exception(message);
