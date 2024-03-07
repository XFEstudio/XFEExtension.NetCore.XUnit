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
                string globalUsingCode = @"global using global::XFEExtension.NetCore.XUnit;";
                init.AddSource("GlobalUsing.g.cs", globalUsingCode);
                string xUnitCode = $@"namespace XFEExtension.NetCore.XUnit
{{
    public static class XUnitCode
    {{
        public async static Task Main(string[] args) => await global::XFEExtension.NetCore.XUnit.XFECode.RunTest();
    }}
}}
";
                init.AddSource($"XUnitCode.g.cs", xUnitCode);
            });
        }

        public void Execute(GeneratorExecutionContext context) { }
    }
}
