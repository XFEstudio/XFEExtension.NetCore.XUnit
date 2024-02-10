using Microsoft.CodeAnalysis;

namespace XFE各类拓展.NetCore.XUnit
{
    [Generator]
    public class XUnitCodeGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // find the main method
            var mainMethod = context.Compilation.GetEntryPoint(context.CancellationToken);

            // build up the source code
            string source = $@"
using XFE各类拓展.NetCore.XUnit;

namespace {mainMethod.ContainingNamespace.Name}
{{
    public static class XUnitCode
    {{
        public async static Task Main(string[] args)
        {{
            await XFECode.RunTest();
        }}
    }}
}}
";
            //var typeName = mainMethod.ContainingType.Name;
            // add the source code to the compilation
            context.AddSource($"XUnitCode.g.cs", source);
        }
    }
}
