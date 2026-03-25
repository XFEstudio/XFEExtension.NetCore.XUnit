using Microsoft.CodeAnalysis;

namespace XFEExtension.NetCore.XUnit.Analyzer.Generator
{
    [Generator]
    public class XUnitCodeGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForPostInitialization(init =>
            {
                const string globalUsingCode = """
                                               global using global::XFEExtension.NetCore.XUnit;
                                               global using global::XFEExtension.NetCore.XUnit.Attributes;
                                               """;
                init.AddSource("GlobalUsing.g.cs", globalUsingCode);
                const string xUnitCode = """
                                         namespace XFEExtension.NetCore.XUnit
                                         {
                                             public static class XUnitCode
                                             {
                                                 public async static Task Main(string[] args)
                                                 {
                                                     await global::XFEExtension.NetCore.XUnit.XFECode.RunTest();
                                                 }
                                             }
                                         }

                                         """;
                init.AddSource($"XUnitCode.g.cs", xUnitCode);
            });
        }

        public void Execute(GeneratorExecutionContext context) { }
    }
}
