using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace XFE各类拓展.NetCore.XUnit.Analyzer.Diagnostics
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class XUnitCodeAnalyzer : DiagnosticAnalyzer
    {
        public const string SMTestID = "XFE0001";
        public static readonly DiagnosticDescriptor SMTestError = new DiagnosticDescriptor(SMTestID,
                                                                                           "不能标记非静态方法",
                                                                                           "SMTest特性不能用在静态方法之外的地方：'{0}'",
                                                                                           "XFE各类拓展.NetCore.XUnit.Analyzer.Diagnostics",
                                                                                           DiagnosticSeverity.Error,
                                                                                           true,
                                                                                           "SMTest特性不能用在静态方法之外的地方.",
                                                                                           "https://www.xfegzs.com/codespace/diagnostics/XFE0001.html");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(SMTestError);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AttributeAnalyzer, SyntaxKind.Attribute);
        }

        public static void AttributeAnalyzer(SyntaxNodeAnalysisContext context)
        {
            var syntaxTrees = context.Compilation.SyntaxTrees;
            foreach (var syntaxTree in syntaxTrees)
            {
                var root = syntaxTree.GetRoot();
                var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                foreach (var methodDeclaration in methodDeclarations)
                {
                    var modifiers = methodDeclaration.Modifiers;
                    bool isStatic = modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword));
                    if (isStatic)
                        continue;
                    var methodName = methodDeclaration.Identifier.ValueText;
                    var attributes = methodDeclaration.AttributeLists.SelectMany(attributeList => attributeList.Attributes);
                    foreach (var attribute in attributes)
                    {
                        if (attribute.Name.ToString() != "SMTest")
                            continue;
                        var diagnostic = Diagnostic.Create(SMTestError, attribute.GetLocation(), methodName);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
