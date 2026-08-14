using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using XFEExtension.NetCore.XUnit.Analyzer.Diagnostics;

namespace XFEExtension.NetCore.XUnit.Analyzer.CodeFix;

/// <summary>
/// 为不可等待的 <see langword="async void"/> 方法和 3.x 旧特性提供迁移代码修复。
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(XUnitCodeFixProvider))]
public sealed class XUnitCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// 获取该提供程序能够修复的诊断标识符。
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds => [XUnitCodeAnalyzer.AsyncVoidId, XUnitCodeAnalyzer.LegacyId];

    /// <summary>
    /// 获取支持文档、项目和解决方案范围批量修复的提供程序。
    /// </summary>
    /// <returns>Roslyn 标准批量修复提供程序。</returns>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// 根据上下文中的诊断注册“返回 Task”或替换旧特性的代码操作。
    /// </summary>
    /// <param name="context">包含文档、诊断和代码操作注册回调的上下文。</param>
    /// <returns>已完成的注册任务。</returns>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            if (diagnostic.Id == XUnitCodeAnalyzer.AsyncVoidId)
                context.RegisterCodeFix(CodeAction.Create("Return Task", token => ChangeAsyncVoidAsync(context.Document, diagnostic, token), "XFE.ReturnTask"), diagnostic);
            else if (diagnostic.Id == XUnitCodeAnalyzer.LegacyId && diagnostic.Properties.TryGetValue("Replacement", out var replacement) && replacement is not null)
                context.RegisterCodeFix(CodeAction.Create($"Use {replacement}", token => ReplaceAttributeAsync(context.Document, diagnostic, replacement, token), "XFE.ReplaceLegacyAttribute"), diagnostic);
        }
        return Task.CompletedTask;
    }

    private static async Task<Document> ChangeAsyncVoidAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root?.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } method)
            return document;
        var updated = method.WithReturnType(SyntaxFactory.ParseTypeName("global::System.Threading.Tasks.Task").WithTriviaFrom(method.ReturnType));
        return document.WithSyntaxRoot(root.ReplaceNode(method, updated));
    }

    private static async Task<Document> ReplaceAttributeAsync(Document document, Diagnostic diagnostic, string replacement, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root?.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<AttributeSyntax>() is not { } attribute)
            return document;
        var arguments = replacement is "Benchmark" or "TestFixture" or "BeforeEach" ? null : attribute.ArgumentList;
        var updated = attribute.WithName(SyntaxFactory.IdentifierName(replacement)).WithArgumentList(arguments).WithTriviaFrom(attribute);
        return document.WithSyntaxRoot(root.ReplaceNode(attribute, updated));
    }
}
