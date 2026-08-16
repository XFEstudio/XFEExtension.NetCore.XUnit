using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace XFEExtension.NetCore.XUnit.Analyzer.Diagnostics;

/// <summary>
/// 对 XFE 测试和基准特性执行语义分析，诊断不可等待的异步方法、无效生命周期签名和 3.x 旧 API。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class XUnitCodeAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeNamespace = "XFEExtension.NetCore.XUnit.Attributes";

    /// <summary>
    /// 获取“测试或基准使用 <see langword="async void"/>”诊断的稳定标识符。
    /// </summary>
    public const string AsyncVoidId = "XFE0002";

    /// <summary>
    /// 获取“生命周期方法签名无效”诊断的稳定标识符。
    /// </summary>
    public const string LifecycleId = "XFE0003";

    /// <summary>
    /// 获取“使用 3.x 旧测试 API”迁移诊断的稳定标识符。
    /// </summary>
    public const string LegacyId = "XFE0100";

    /// <summary>
    /// 获取拒绝不可等待 <see langword="async void"/> 测试和基准的错误规则。
    /// </summary>
    public static readonly DiagnosticDescriptor AsyncVoidRule = new(
        AsyncVoidId, "async void tests cannot be awaited",
        "Method '{0}' is async void; return Task or ValueTask so the runner can await completion",
        "XFEExtension.NetCore.XUnit", DiagnosticSeverity.Error, true);

    /// <summary>
    /// 获取验证生命周期方法静态性和参数列表的错误规则。
    /// </summary>
    public static readonly DiagnosticDescriptor LifecycleRule = new(
        LifecycleId, "Invalid lifecycle method",
        "Lifecycle method '{0}' must be parameterless{1}",
        "XFEExtension.NetCore.XUnit", DiagnosticSeverity.Error, true);

    /// <summary>
    /// 获取提示迁移 3.x 特性的警告规则。
    /// </summary>
    public static readonly DiagnosticDescriptor LegacyRule = new(
        LegacyId, "Legacy XFE test API",
        "'{0}' is obsolete and will be removed in XUnit 5.0; migrate to '{1}'",
        "XFEExtension.NetCore.XUnit", DiagnosticSeverity.Warning, true);

    /// <summary>
    /// 获取该分析器能够产生的全部诊断描述符。
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [AsyncVoidRule, LifecycleRule, LegacyRule];

    /// <summary>
    /// 注册并发符号分析操作，并排除生成代码。
    /// </summary>
    /// <param name="context">Roslyn 分析器初始化上下文。</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        var attributes = method.GetAttributes().Where(IsXfeAttribute).ToArray();
        if (attributes.Length == 0)
            return;
        if (method.IsAsync && method.ReturnsVoid && attributes.Any(static attribute => IsExecutionAttribute(attribute.AttributeClass)))
            context.ReportDiagnostic(Diagnostic.Create(AsyncVoidRule, method.Locations.FirstOrDefault(), method.Name));

        foreach (var attribute in attributes)
        {
            var name = attribute.AttributeClass?.Name;
            if (name is "BeforeAllAttribute" or "AfterAllAttribute" && (!method.IsStatic || method.Parameters.Length != 0))
                context.ReportDiagnostic(Diagnostic.Create(LifecycleRule, method.Locations.FirstOrDefault(), method.Name, " and static"));
            else if (name is "BeforeEachAttribute" or "AfterEachAttribute" or "GlobalSetupAttribute" or "GlobalCleanupAttribute" or "IterationSetupAttribute" or "IterationCleanupAttribute" && method.Parameters.Length != 0)
                context.ReportDiagnostic(Diagnostic.Create(LifecycleRule, method.Locations.FirstOrDefault(), method.Name, string.Empty));
            ReportLegacy(context, attribute);
        }
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        foreach (var attribute in ((INamedTypeSymbol)context.Symbol).GetAttributes().Where(IsXfeAttribute))
            ReportLegacy(context, attribute);
    }

    private static void ReportLegacy(SymbolAnalysisContext context, AttributeData attribute)
    {
        var name = attribute.AttributeClass?.Name;
        var replacement = name switch
        {
            "CTestAttribute" or "CNTestAttribute" => "TestFixtureAttribute",
            "MTestAttribute" or "MNTestAttribute" or "MRTestAttribute" or "MNRTestAttribute" => "TestCaseAttribute",
            "SMTestAttribute" or "SMNTestAttribute" or "SMRTestAttribute" or "SMNRTestAttribute" => "TestCaseAttribute",
            "SetUpAttribute" => "BeforeEachAttribute",
            _ => null
        };
        if (replacement is null)
            return;
        var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation() ?? context.Symbol.Locations.FirstOrDefault();
        var properties = ImmutableDictionary<string, string?>.Empty.Add("Replacement", replacement.Replace("Attribute", string.Empty));
        context.ReportDiagnostic(Diagnostic.Create(LegacyRule, location, properties, name!.Replace("Attribute", string.Empty), replacement.Replace("Attribute", string.Empty)));
    }

    private static bool IsXfeAttribute(AttributeData attribute) => attribute.AttributeClass?.ContainingNamespace.ToDisplayString() == AttributeNamespace;

    private static bool IsExecutionAttribute(INamedTypeSymbol? type)
    {
        while (type is not null)
        {
            if (type.ContainingNamespace.ToDisplayString() == AttributeNamespace && type.Name is "TestAttribute" or "TestCaseAttribute" or "MTestAttribute" or "SMTestAttribute" or "BenchmarkAttribute")
                return true;
            type = type.BaseType;
        }
        return false;
    }
}
