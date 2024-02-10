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
            string source = $@"
using XFE各类拓展.NetCore.XUnit;

namespace XFE各类拓展.NetCore.XUnit
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
            context.AddSource($"XUnitCode.g.cs", source);
        }
    }
}
