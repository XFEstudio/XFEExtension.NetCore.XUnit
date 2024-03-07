using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XFE各类拓展.NetCore.XUnit.Analyzer.Diagnostics;

namespace XFE各类拓展.NetCore.XUnit.Analyzer.CodeFix
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(XUnitCodeFixProvider))]
    public class XUnitCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(XUnitCodeAnalyzer.SMTestID);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            foreach (var diagnostic in context.Diagnostics)
            {
                if (diagnostic.Id == XUnitCodeAnalyzer.SMTestID)
                {
                    context.RegisterCodeFix(CodeAction.Create(title: "改用MTest特性",
                                                              createChangedDocument: c => ChangeSMTestToMTestAsync(context.Document, diagnostic, c),
                                                              equivalenceKey: "改用MTest特性"),
                                                              diagnostic: diagnostic);
                    context.RegisterCodeFix(CodeAction.Create(title: "将方法改为静态方法",
                                                              createChangedDocument: c => MakeMethodStaticAsync(context.Document, diagnostic, c),
                                                              equivalenceKey: "将方法改为静态方法"),
                                                              diagnostic: diagnostic);
                }
            }
            return Task.CompletedTask;
        }

        public static async Task<Document> MakeMethodStaticAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var attribute = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent.AncestorsAndSelf().OfType<AttributeSyntax>().First();
            var method = attribute.Parent.Parent as MethodDeclarationSyntax;
            var newMethod = method.AddModifiers(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
            var newRoot = root.ReplaceNode(method, newMethod);
            return document.WithSyntaxRoot(newRoot);
        }

        public static async Task<Document> ChangeSMTestToMTestAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var attribute = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent.AncestorsAndSelf().OfType<AttributeSyntax>().First();
            var newAttribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("MTest"));
            var newRoot = root.ReplaceNode(attribute, newAttribute);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
