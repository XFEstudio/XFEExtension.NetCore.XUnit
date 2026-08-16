using System.Text;

namespace XFEExtension.NetCore.XUnit.Execution;

internal sealed class AsyncLocalConsoleCapture : TextWriter
{
    private static readonly AsyncLocal<CaptureBuffer?> s_current = new();
    private readonly TextWriter _fallback;
    private static AsyncLocalConsoleCapture? s_outputInstance;
    private static AsyncLocalConsoleCapture? s_errorInstance;

    private AsyncLocalConsoleCapture(TextWriter fallback) => _fallback = fallback;

    public override Encoding Encoding => _fallback.Encoding;

    public static void Install()
    {
        if (s_outputInstance is not null)
            return;
        s_outputInstance = new AsyncLocalConsoleCapture(Console.Out);
        s_errorInstance = new AsyncLocalConsoleCapture(Console.Error);
        Console.SetOut(s_outputInstance);
        Console.SetError(s_errorInstance);
    }

    public static IDisposable Begin(out Func<string> getOutput)
    {
        var previous = s_current.Value;
        var buffer = new CaptureBuffer();
        s_current.Value = buffer;
        getOutput = buffer.GetText;
        return new CaptureScope(previous);
    }

    public override void Write(char value)
    {
        if (s_current.Value is { } buffer)
            buffer.Append(value);
        else
            _fallback.Write(value);
    }

    public override void Write(string? value)
    {
        if (s_current.Value is { } buffer)
            buffer.Append(value);
        else
            _fallback.Write(value);
    }

    public override void WriteLine(string? value)
    {
        if (s_current.Value is { } buffer)
            buffer.AppendLine(value);
        else
            _fallback.WriteLine(value);
    }

    private sealed class CaptureScope(CaptureBuffer? previous) : IDisposable
    {
        public void Dispose() => s_current.Value = previous;
    }

    private sealed class CaptureBuffer
    {
        private readonly Lock _lock = new();
        private readonly StringBuilder _builder = new();

        public void Append(char value)
        {
            lock (_lock)
                _builder.Append(value);
        }

        public void Append(string? value)
        {
            lock (_lock)
                _builder.Append(value);
        }

        public void AppendLine(string? value)
        {
            lock (_lock)
                _builder.AppendLine(value);
        }

        public string GetText()
        {
            lock (_lock)
                return _builder.ToString();
        }
    }
}
