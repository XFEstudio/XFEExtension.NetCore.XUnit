using Microsoft.CodeAnalysis;

namespace XFE各类拓展.NetCore.XUnit
{
    [Generator]
    public class XUnitCodeGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForPostInitialization(init =>
            {
                string globalUsingCode = @"global using XFE各类拓展.NetCore.XUnit;";
                init.AddSource("GlobalUsing.g.cs", globalUsingCode);
                string xUnitCode = $@"namespace XFE各类拓展.NetCore.XUnit
{{
    public static class XUnitCode
    {{
        public async static Task Main(string[] args) => await global::XFE各类拓展.NetCore.XUnit.XFECode.RunTest();
    }}
}}
";
                init.AddSource($"XUnitCode.g.cs", xUnitCode);
            });
        }

        public void Execute(GeneratorExecutionContext context) { }
    }
}
