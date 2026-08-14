using System.Text;

namespace XFEExtension.NetCore.XUnit;

internal sealed class AsyncLocalConsoleCapture : TextWriter
{
    private static readonly AsyncLocal<StringBuilder?> s_current = new();
    private readonly TextWriter _fallback;
    private static AsyncLocalConsoleCapture? s_instance;

    private AsyncLocalConsoleCapture(TextWriter fallback) => _fallback = fallback;

    public override Encoding Encoding => _fallback.Encoding;

    public static void Install()
    {
        if (s_instance is not null)
            return;
        s_instance = new AsyncLocalConsoleCapture(Console.Out);
        Console.SetOut(s_instance);
    }

    public static IDisposable Begin(out Func<string> getOutput)
    {
        var previous = s_current.Value;
        var builder = new StringBuilder();
        s_current.Value = builder;
        getOutput = builder.ToString;
        return new CaptureScope(previous);
    }

    public override void Write(char value)
    {
        if (s_current.Value is { } builder)
            builder.Append(value);
        else
            _fallback.Write(value);
    }

    public override void Write(string? value)
    {
        if (s_current.Value is { } builder)
            builder.Append(value);
        else
            _fallback.Write(value);
    }

    public override void WriteLine(string? value)
    {
        if (s_current.Value is { } builder)
            builder.AppendLine(value);
        else
            _fallback.WriteLine(value);
    }

    private sealed class CaptureScope(StringBuilder? previous) : IDisposable
    {
        public void Dispose() => s_current.Value = previous;
    }
}
