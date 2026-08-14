namespace XFEExtension.NetCore.XUnit.Execution;

public static partial class XFERunner
{
    private sealed class XFEConfigurationException(string message) : Exception(message);
}
