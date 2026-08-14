using System.Globalization;
using System.Text.RegularExpressions;
using XFEExtension.NetCore.XUnit.Runtime;

namespace XFEExtension.NetCore.XUnit.Reporting;

internal sealed partial class ConsoleLocalizer
{
    public ConsoleLocalizer(ConsoleLanguage requestedLanguage)
    {
        RequestedLanguage = requestedLanguage;
        Language = requestedLanguage == ConsoleLanguage.Auto ? DetectLanguage() : requestedLanguage;
    }

    public ConsoleLanguage RequestedLanguage { get; }

    public ConsoleLanguage Language { get; }

    public bool IsChinese => Language == ConsoleLanguage.Chinese;

    public string Select(string english, string chinese) => IsChinese ? chinese : english;

    public string LanguageName => RequestedLanguage == ConsoleLanguage.Auto
        ? Select("English (auto)", "简体中文（自动）")
        : Select("English", "简体中文");

    public string Outcome(TestOutcome outcome) => outcome switch
    {
        TestOutcome.Passed => Select("PASS", "通过"),
        TestOutcome.Failed => Select("FAIL", "失败"),
        TestOutcome.Skipped => Select("SKIP", "跳过"),
        TestOutcome.TimedOut => Select("TIMEOUT", "超时"),
        TestOutcome.Crashed => Select("CRASH", "崩溃"),
        _ => outcome.ToString().ToUpperInvariant()
    };

    public string Message(string message)
    {
        if (!IsChinese)
            return message;
        if (message == "Explicit test was not selected.")
            return "未选择显式测试。";
        if (message == "Only one ITestActivator extension can be registered per test assembly.")
            return "每个测试程序集只能注册一个 ITestActivator 扩展。";
        if (message == "The benchmark baseline file is invalid.")
            return "基准基线文件无效。";
        if (message == "Benchmark regression gating was refused because the environments differ.")
            return "由于运行环境不同，已拒绝执行基准回归门禁。";
        var timeout = Regex.Match(message, @"^Test exceeded the (?<milliseconds>\d+) ms timeout(?<terminated> and its worker process was terminated)?\.$", RegexOptions.CultureInvariant);
        if (timeout.Success)
            return timeout.Groups["terminated"].Success
                ? $"测试超过 {timeout.Groups["milliseconds"].Value} ms 超时限制，工作进程已终止。"
                : $"测试超过 {timeout.Groups["milliseconds"].Value} ms 超时限制。";
        var worker = Regex.Match(message, @"^Worker exited with code (?<code>-?\d+) without producing a result\.$", RegexOptions.CultureInvariant);
        if (worker.Success)
            return $"工作进程以代码 {worker.Groups["code"].Value} 退出，且未生成结果。";
        const string missingBaseline = "Benchmark baseline file was not found: ";
        if (message.StartsWith(missingBaseline, StringComparison.Ordinal))
            return "未找到基准基线文件：" + message[missingBaseline.Length..];
        const string missingSettings = "Run settings file was not found: ";
        if (message.StartsWith(missingSettings, StringComparison.Ordinal))
            return "未找到运行设置文件：" + message[missingSettings.Length..];
        const string gatePrefix = "Cannot gate ";
        const string gateSuffix = ": baseline environment fingerprint differs.";
        if (message.StartsWith(gatePrefix, StringComparison.Ordinal) && message.EndsWith(gateSuffix, StringComparison.Ordinal))
            return $"无法对 {message[gatePrefix.Length..^gateSuffix.Length]} 执行门禁：基线环境指纹不同。";
        return message;
    }

    public string Warning(string warning)
    {
        if (!IsChinese)
            return warning;
        if (warning.StartsWith("DebuggerAttached", StringComparison.Ordinal))
            return "已附加调试器：调试器会使基准测量结果不可靠。";
        if (warning.StartsWith("NonOptimizedAssembly", StringComparison.Ordinal))
            return "程序集未优化：请使用 Release 配置运行基准。";
        if (warning.StartsWith("LowResolutionClock", StringComparison.Ordinal))
            return "低分辨率计时器：Stopwatch 未使用高分辨率性能计数器。";
        if (warning.StartsWith("HighNoise", StringComparison.Ordinal))
            return "噪声过高：标准差超过均值的 10%。";
        if (warning.StartsWith("MeasurementTrend", StringComparison.Ordinal))
            return "测量趋势：样本呈现显著的时间相关趋势。";
        if (warning.StartsWith("DistributionSkew", StringComparison.Ordinal))
            return "分布偏斜：均值与中位数相差超过 5%。";
        if (warning.StartsWith("BelowTimerResolution", StringComparison.Ordinal))
            return "低于计时分辨率：扣除基础设施开销后的工作量已低于可靠测量范围。";
        if (warning.StartsWith("NotConverged", StringComparison.Ordinal))
        {
            var percentages = PercentageRegex().Matches(warning).Select(static match => match.Value).ToArray();
            return percentages.Length >= 2
                ? $"未收敛：相对误差 {percentages[0]} 超出目标 {percentages[1]}。"
                : "未收敛：样本未在允许的迭代次数内达到误差目标。";
        }
        return warning;
    }

    public static bool TryParseLanguage(string? value, out ConsoleLanguage language)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "auto":
            case "automatic":
                language = ConsoleLanguage.Auto;
                return true;
            case "en":
            case "en-us":
            case "en-gb":
            case "english":
                language = ConsoleLanguage.English;
                return true;
            case "zh":
            case "zh-cn":
            case "zh-hans":
            case "chinese":
            case "简体中文":
            case "中文":
                language = ConsoleLanguage.Chinese;
                return true;
            default:
                language = ConsoleLanguage.Auto;
                return false;
        }
    }

    private static ConsoleLanguage DetectLanguage()
    {
        var uiCulture = CultureInfo.CurrentUICulture.Name;
        if (uiCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return ConsoleLanguage.Chinese;
        if (!string.IsNullOrWhiteSpace(uiCulture))
            return ConsoleLanguage.English;
        return CultureInfo.CurrentCulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? ConsoleLanguage.Chinese
            : ConsoleLanguage.English;
    }

    [GeneratedRegex(@"\d+(?:[\.,]\d+)?\s*%", RegexOptions.CultureInvariant)]
    private static partial Regex PercentageRegex();
}
