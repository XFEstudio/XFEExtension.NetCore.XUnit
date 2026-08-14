using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace XFEExtension.NetCore.XUnit.Analyzer.Generator;

/// <summary>
/// 以语义方式发现 XFE 测试、基准和生命周期方法，并生成注册表、直接调用器与可选入口点。
/// </summary>
[Generator]
public sealed class XUnitCodeGenerator : IIncrementalGenerator
{
    private const string AttributeNamespace = "XFEExtension.NetCore.XUnit.Attributes";

    private static readonly DiagnosticDescriptor UnsupportedSignature = new(
        "XFE1001", "Unsupported XFE test signature",
        "Method '{0}' cannot be generated: {1}", "XFEExtension.NetCore.XUnit.Generator",
        DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor ExistingEntryPoint = new(
        "XFE1002", "XFE entry point was not generated",
        "The project already has an entry point; call await XFERunner.RunAsync(args) from it",
        "XFEExtension.NetCore.XUnit.Generator", DiagnosticSeverity.Info, true);

    private static readonly DiagnosticDescriptor InvalidArguments = new(
        "XFE1003", "Invalid XFE test arguments",
        "Method '{0}' declares {1} parameter(s), but '{2}' supplies {3} argument(s)",
        "XFEExtension.NetCore.XUnit.Generator", DiagnosticSeverity.Error, true);

    /// <summary>
    /// 配置增量语法提供器、全局 using 输出以及基于编译和候选方法的注册表生成管线。
    /// </summary>
    /// <param name="context">Roslyn 增量生成器初始化上下文。</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static output => output.AddSource("XfeGlobalUsings.g.cs", SourceText.From(
            "global using global::XFEExtension.NetCore.XUnit;\n" +
            "global using global::XFEExtension.NetCore.XUnit.Attributes;\n", Encoding.UTF8)));

        var methods = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is MethodDeclarationSyntax method && method.AttributeLists.Count > 0,
                static (syntaxContext, cancellationToken) => syntaxContext.SemanticModel.GetDeclaredSymbol((MethodDeclarationSyntax)syntaxContext.Node, cancellationToken) as IMethodSymbol)
            .Where(static method => method is not null)
            .Collect();

        context.RegisterSourceOutput(context.CompilationProvider.Combine(methods), static (output, input) =>
            Generate(output, input.Left, input.Right!));
    }

    private static void Generate(SourceProductionContext context, Compilation compilation, ImmutableArray<IMethodSymbol?> candidates)
    {
        var methods = candidates.Where(static method => method is not null)
            .Cast<IMethodSymbol>()
            .Where(IsRelevant)
            .ToArray();
        var source = new StringBuilder();
        var registrations = new StringBuilder();
        var wrappers = new Dictionary<IMethodSymbol, string>(SymbolEqualityComparer.Default);
        var overheadWrappers = new Dictionary<IMethodSymbol, string>(SymbolEqualityComparer.Default);
        var wrapperOrder = new List<IMethodSymbol>();

        string Wrapper(IMethodSymbol method)
        {
            if (wrappers.TryGetValue(method, out var existing))
                return existing;
            var name = "Invoke_" + wrappers.Count;
            wrappers.Add(method, name);
            wrapperOrder.Add(method);
            return name;
        }

        var relevantTypes = methods.Select(static method => method.ContainingType).Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>().ToArray();
        var lifecycleByType = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
        foreach (var type in relevantTypes)
            lifecycleByType[type] = BuildLifecycle(type, Wrapper, context);

        foreach (var method in methods)
        {
            if (!ValidateMethod(method, context))
                continue;
            var wrapper = Wrapper(method);
            BuildTestRegistrations(registrations, method, wrapper, lifecycleByType[method.ContainingType]);
            var overheadWrapper = "Overhead_" + overheadWrappers.Count;
            overheadWrappers[method] = overheadWrapper;
            BuildBenchmarkRegistrations(registrations, method, wrapper, overheadWrapper, lifecycleByType[method.ContainingType]);
        }

        source.AppendLine("#nullable enable");
        source.AppendLine("namespace XFEExtension.NetCore.XUnit.Generated;");
        source.AppendLine("internal static class XfeGeneratedTestRegistry");
        source.AppendLine("{");
        source.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        source.AppendLine("    internal static void Initialize() => global::XFEExtension.NetCore.XUnit.Runtime.XfeGeneratedRegistry.SetFactory(Create);");
        source.AppendLine("    internal static global::XFEExtension.NetCore.XUnit.Runtime.XfeRegistry Create()");
        source.AppendLine("    {");
        source.AppendLine("        var registry = new global::XFEExtension.NetCore.XUnit.Runtime.XfeRegistry();");
        source.Append(registrations);
        source.AppendLine("        return registry;");
        source.AppendLine("    }");

        foreach (var method in wrapperOrder)
        {
            source.AppendLine(BuildWrapper(method, wrappers[method]));
            source.AppendLine(BuildOverheadWrapper(method, overheadWrappers[method]));
        }
        source.AppendLine("}");
        context.AddSource("XfeGeneratedRegistry.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));

        if (compilation.GetEntryPoint(context.CancellationToken) is null)
        {
            context.AddSource("XfeEntryPoint.g.cs", SourceText.From(
                "namespace XFEExtension.NetCore.XUnit.Generated;\n" +
                "internal static class XfeEntryPoint\n{\n" +
                "    public static global::System.Threading.Tasks.Task<int> Main(string[] args) => global::XFEExtension.NetCore.XUnit.Execution.XFERunner.RunAsync(args);\n" +
                "}\n", Encoding.UTF8));
        }
        else
        {
            context.ReportDiagnostic(Diagnostic.Create(ExistingEntryPoint, Location.None));
        }
    }

    private static bool IsRelevant(IMethodSymbol method) => method.GetAttributes().Any(static attribute => IsXfeAttribute(attribute.AttributeClass));

    private static bool IsXfeAttribute(INamedTypeSymbol? type)
    {
        while (type is not null)
        {
            if (type.ContainingNamespace.ToDisplayString() == AttributeNamespace)
                return true;
            type = type.BaseType;
        }
        return false;
    }

    private static bool ValidateMethod(IMethodSymbol method, SourceProductionContext context)
    {
        string? reason = null;
        if (method.IsAsync && method.ReturnsVoid)
            reason = "async void is not awaitable; return Task or ValueTask";
        else if (method.IsGenericMethod)
            reason = "generic test methods are not supported";
        else if (method.Parameters.Any(static parameter => parameter.RefKind != RefKind.None))
            reason = "ref, in and out parameters are not supported";
        else if (method.ReturnsByRef || method.ReturnsByRefReadonly)
            reason = "ref return values are not supported";
        else if (method.DeclaredAccessibility == Accessibility.Private)
            reason = "private methods cannot use the generated direct-call path; make the method internal or public";
        else if (method.ContainingType.IsGenericType)
            reason = "open or constructed generic test fixtures are not supported";
        if (reason is null)
        {
            var executionAttributes = method.GetAttributes().Where(static attribute =>
                attribute.AttributeClass?.Name is "TestCaseAttribute" or "ArgumentsAttribute" ||
                Inherits(attribute.AttributeClass, "MTestAttribute") || Inherits(attribute.AttributeClass, "SMTestAttribute"));
            foreach (var attribute in executionAttributes)
            {
                var legacy = Inherits(attribute.AttributeClass, "MTestAttribute") || Inherits(attribute.AttributeClass, "SMTestAttribute");
                var supplied = legacy
                    ? GetLegacyArguments(attribute, Inherits(attribute.AttributeClass, "SMTestAttribute"), out _, out _, out _).Length
                    : GetArrayArgument(attribute, 0).Length;
                if (supplied != method.Parameters.Length)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidArguments, method.Locations.FirstOrDefault(), method.Name, method.Parameters.Length,
                        attribute.AttributeClass?.Name.Replace("Attribute", string.Empty), supplied));
                    return false;
                }
            }
            var hasDirectTest = FindAttribute(method, "TestAttribute") is not null && !FindAttributes(method, "TestCaseAttribute").Any() && !FindAttributes(method, "MemberDataAttribute").Any();
            var hasDirectBenchmark = FindAttribute(method, "BenchmarkAttribute") is not null && !FindAttributes(method, "ArgumentsAttribute").Any();
            if ((hasDirectTest || hasDirectBenchmark) && method.Parameters.Length != 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidArguments, method.Locations.FirstOrDefault(), method.Name, method.Parameters.Length,
                    hasDirectTest ? "Test" : "Benchmark", 0));
                return false;
            }
            return true;
        }
        context.ReportDiagnostic(Diagnostic.Create(UnsupportedSignature, method.Locations.FirstOrDefault(), method.ToDisplayString(), reason));
        return false;
    }

    private static void BuildTestRegistrations(StringBuilder output, IMethodSymbol method, string wrapper, string lifecycle)
    {
        var test = FindAttribute(method, "TestAttribute");
        var testCases = FindAttributes(method, "TestCaseAttribute").ToArray();
        var memberData = FindAttributes(method, "MemberDataAttribute").ToArray();
        var legacyCases = method.GetAttributes().Where(static attribute => Inherits(attribute.AttributeClass, "MTestAttribute")).ToArray();
        var classCases = method.ContainingType.GetAttributes().Where(static attribute => Inherits(attribute.AttributeClass, "CTestAttribute")).ToArray();
        var index = 0;

        if (test is not null && testCases.Length == 0 && memberData.Length == 0)
            AppendTest(output, method, wrapper, lifecycle, "[]", index++, GetNamedString(test, "Name"), false, false, "null", "static () => global::XFEExtension.NetCore.XUnit.Runtime.XfeObjectFactory.Create(typeof(" + TypeName(method.ContainingType) + "), [])");
        foreach (var attribute in testCases)
            AppendTest(output, method, wrapper, lifecycle, ArrayExpression(GetArrayArgument(attribute, 0)), index++, GetNamedString(attribute, "Name"), false, false, "null", "static () => global::XFEExtension.NetCore.XUnit.Runtime.XfeObjectFactory.Create(typeof(" + TypeName(method.ContainingType) + "), [])");

        foreach (var attribute in memberData)
        {
            var memberName = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string : null;
            if (memberName is null)
                continue;
            var sourceType = attribute.NamedArguments.FirstOrDefault(static pair => pair.Key == "MemberType").Value.Value as INamedTypeSymbol ?? method.ContainingType;
            var prefix = Escape(Id(method, index++));
            output.Append("        { var dataIndex = 0; foreach (var data in global::XFEExtension.NetCore.XUnit.Runtime.XfeMemberData.Get(typeof(")
                .Append(TypeName(sourceType)).Append("), ").Append(Escape(memberName)).AppendLine(")) {");
            AppendTest(output, method, wrapper, lifecycle, "data", 0, null, false, false, "null",
                "static () => global::XFEExtension.NetCore.XUnit.Runtime.XfeObjectFactory.Create(typeof(" + TypeName(method.ContainingType) + "), [])", prefix + " + \"-\" + dataIndex",
                Escape(method.ContainingType.Name + "." + method.Name + "[") + " + dataIndex + \"]\"", 12);
            output.AppendLine("            dataIndex++;");
            output.AppendLine("        } }");
        }

        if (legacyCases.Length > 0 && classCases.Length > 0)
        {
            foreach (var classCase in classCases)
            foreach (var attribute in legacyCases)
            {
                var arguments = GetLegacyArguments(attribute, false, out var expected, out var hasExpected, out var name);
                var constructorArguments = GetArrayArgument(classCase, classCase.AttributeClass?.Name == "CNTestAttribute" ? 1 : 0);
                var factory = "static () => global::XFEExtension.NetCore.XUnit.Runtime.XfeObjectFactory.Create(typeof(" + TypeName(method.ContainingType) + "), " + ArrayExpression(constructorArguments) + ")";
                AppendTest(output, method, wrapper, lifecycle, ArrayExpression(arguments), index++, name, true, hasExpected, expected, factory);
            }
        }
    }

    private static void AppendTest(StringBuilder output, IMethodSymbol method, string wrapper, string lifecycle, string arguments, int index,
        string? name, bool legacy, bool hasExpected, string expected, string factory, string? customId = null, string? customDisplay = null, int indent = 8)
    {
        var spaces = new string(' ', indent);
        var id = customId ?? Escape(Id(method, index));
        output.Append(spaces).AppendLine("registry.AddTest(new global::XFEExtension.NetCore.XUnit.Runtime.TestDescriptor");
        output.Append(spaces).AppendLine("{");
        output.Append(spaces).Append("    Id = ").Append(id).AppendLine(",");
        var display = customDisplay ?? Escape(name ?? method.ContainingType.Name + "." + method.Name + (arguments == "[]" ? string.Empty : "#" + index));
        output.Append(spaces).Append("    DisplayName = ").Append(display).AppendLine(",");
        output.Append(spaces).Append("    TypeName = ").Append(Escape(method.ContainingType.ToDisplayString())).AppendLine(",");
        output.Append(spaces).Append("    MethodName = ").Append(Escape(method.Name)).AppendLine(",");
        output.Append(spaces).Append("    Arguments = ").Append(arguments).AppendLine(",");
        output.Append(spaces).Append("    Categories = ").Append(StringArray(GetCategories(method))).AppendLine(",");
        output.Append(spaces).Append("    Traits = ").Append(TraitDictionary(method)).AppendLine(",");
        output.Append(spaces).Append("    SkipReason = ").Append(NullableString(GetInheritedString(method, "SkipAttribute"))).AppendLine(",");
        output.Append(spaces).Append("    Explicit = ").Append(HasInheritedAttribute(method, "ExplicitAttribute") ? "true" : "false").AppendLine(",");
        output.Append(spaces).Append("    NonParallel = ").Append(HasInheritedAttribute(method, "NonParallelAttribute") ? "true" : "false").AppendLine(",");
        output.Append(spaces).Append("    Isolated = ").Append(HasInheritedAttribute(method, "IsolatedAttribute") ? "true" : "false").AppendLine(",");
        output.Append(spaces).Append("    Collection = ").Append(NullableString(GetTypeString(method.ContainingType, "CollectionAttribute"))).AppendLine(",");
        output.Append(spaces).Append("    TimeoutMilliseconds = ").Append(GetInt(method, "TimeoutAttribute")).AppendLine(",");
        output.Append(spaces).Append("    RetryCount = ").Append(GetInt(method, "RetryAttribute")).AppendLine(",");
        output.Append(spaces).Append("    IsLegacy = ").Append(legacy ? "true" : "false").AppendLine(",");
        output.Append(spaces).Append("    HasExpectedResult = ").Append(hasExpected ? "true" : "false").AppendLine(",");
        output.Append(spaces).Append("    ExpectedResult = ").Append(expected).AppendLine(",");
        output.Append(spaces).Append("    Factory = ").Append(factory).AppendLine(",");
        output.Append(spaces).Append("    Invoker = ").Append(wrapper).AppendLine(",");
        output.Append(spaces).Append("    Lifecycle = ").Append(lifecycle).AppendLine();
        output.Append(spaces).AppendLine("});");
    }

    private static void BuildBenchmarkRegistrations(StringBuilder output, IMethodSymbol method, string wrapper, string overheadWrapper, string lifecycle)
    {
        var benchmark = FindAttribute(method, "BenchmarkAttribute");
        var legacy = method.GetAttributes().Where(static attribute => Inherits(attribute.AttributeClass, "SMTestAttribute")).ToArray();
        if (benchmark is null && legacy.Length == 0)
            return;
        var argumentSets = benchmark is null
            ? legacy.Select(attribute => GetLegacyArguments(attribute, true, out _, out _, out _)).ToArray()
            : FindAttributes(method, "ArgumentsAttribute").Select(attribute => GetArrayArgument(attribute, 0)).DefaultIfEmpty(ImmutableArray<TypedConstant>.Empty).ToArray();
        var parameterSets = BuildParameterSets(method.ContainingType);
        var caseIndex = 0;
        foreach (var arguments in argumentSets)
        foreach (var parameters in parameterSets)
        {
            var legacyAttribute = benchmark is null ? legacy[Math.Min(caseIndex, legacy.Length - 1)] : null;
            var legacyName = legacyAttribute is null ? null : GetLegacyName(legacyAttribute);
            var displayName = GetNamedString(benchmark, "Name") ?? legacyName ?? method.ContainingType.Name + "." + method.Name;
            if (parameters.Count > 0)
                displayName += "(" + string.Join(", ", parameters.Select(static pair => pair.Key + "=" + pair.Value.DisplayValue)) + ")";
            output.AppendLine("        registry.AddBenchmark(new global::XFEExtension.NetCore.XUnit.Runtime.BenchmarkDescriptor");
            output.AppendLine("        {");
            output.Append("            Id = ").Append(Escape(Id(method, caseIndex))).AppendLine(",");
            output.Append("            DisplayName = ").Append(Escape(displayName)).AppendLine(",");
            output.Append("            TypeName = ").Append(Escape(method.ContainingType.ToDisplayString())).AppendLine(",");
            output.Append("            MethodName = ").Append(Escape(method.Name)).AppendLine(",");
            output.Append("            Arguments = ").Append(ArrayExpression(arguments)).AppendLine(",");
            output.Append("            Categories = ").Append(StringArray(GetCategories(method))).AppendLine(",");
            output.Append("            Baseline = ").Append(GetNamedBool(benchmark, "Baseline") ? "true" : "false").AppendLine(",");
            output.Append("            Strategy = (global::XFEExtension.NetCore.XUnit.Attributes.BenchmarkStrategy)").Append(GetNamedInt(benchmark, "Strategy")).AppendLine(",");
            output.Append("            IsLegacy = ").Append(benchmark is null ? "true" : "false").AppendLine(",");
            output.Append("            ParameterKey = ").Append(Escape(string.Join(";", parameters.Select(static pair => pair.Key + "=" + pair.Value.DisplayValue)))).AppendLine(",");
            output.Append("            Factory = static () => global::XFEExtension.NetCore.XUnit.Runtime.XfeObjectFactory.Create(typeof(").Append(TypeName(method.ContainingType)).AppendLine("), []),");
            output.Append("            Invoker = ").Append(wrapper).AppendLine(",");
            output.Append("            OverheadInvoker = ").Append(overheadWrapper).AppendLine(",");
            output.Append("            ApplyParameters = static instance => {");
            foreach (var parameter in parameters)
                output.Append(" global::XFEExtension.NetCore.XUnit.Runtime.XfeParameterBinder.Set(instance, ").Append(Escape(parameter.Key)).Append(", ").Append(parameter.Value.Expression).Append(");");
            output.AppendLine(" },");
            output.Append("            Lifecycle = ").Append(lifecycle).AppendLine();
            output.AppendLine("        });");
            caseIndex++;
        }
    }

    private static string BuildLifecycle(INamedTypeSymbol type, Func<IMethodSymbol, string> wrapper, SourceProductionContext context)
    {
        string List(string attributeName, bool mustBeStatic)
        {
            var names = new List<string>();
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(member => FindAttribute(member, attributeName) is not null))
            {
                if (method.Parameters.Length > 0 || mustBeStatic && !method.IsStatic || !ValidateMethod(method, context))
                {
                    if (method.Parameters.Length > 0 || mustBeStatic && !method.IsStatic)
                        context.ReportDiagnostic(Diagnostic.Create(UnsupportedSignature, method.Locations.FirstOrDefault(), method.ToDisplayString(),
                            mustBeStatic ? "this lifecycle method must be static and parameterless" : "lifecycle methods must be parameterless"));
                    continue;
                }
                names.Add(wrapper(method));
            }
            return "new global::XFEExtension.NetCore.XUnit.Runtime.XfeInvoker[] { " + string.Join(", ", names) + " }";
        }

        return "new global::XFEExtension.NetCore.XUnit.Runtime.XfeLifecycleHooks { " +
            "BeforeAll = " + List("BeforeAllAttribute", true) + ", " +
            "AfterAll = " + List("AfterAllAttribute", true) + ", " +
            "BeforeEach = " + MergeLists(List("BeforeEachAttribute", false), List("SetUpAttribute", false)) + ", " +
            "AfterEach = " + List("AfterEachAttribute", false) + ", " +
            "GlobalSetup = " + List("GlobalSetupAttribute", false) + ", " +
            "GlobalCleanup = " + List("GlobalCleanupAttribute", false) + ", " +
            "IterationSetup = " + List("IterationSetupAttribute", false) + ", " +
            "IterationCleanup = " + List("IterationCleanupAttribute", false) + " }";
    }

    private static string MergeLists(string left, string right)
    {
        var leftItems = left.Substring(left.IndexOf('{') + 1).TrimEnd(' ', '}');
        var rightItems = right.Substring(right.IndexOf('{') + 1).TrimEnd(' ', '}');
        var values = new[] { leftItems, rightItems }.Where(static value => !string.IsNullOrWhiteSpace(value));
        return "new global::XFEExtension.NetCore.XUnit.Runtime.XfeInvoker[] { " + string.Join(", ", values) + " }";
    }

    private static string BuildWrapper(IMethodSymbol method, string name)
    {
        var target = method.IsStatic ? TypeName(method.ContainingType) : "((" + TypeName(method.ContainingType) + ")instance!)";
        var arguments = string.Join(", ", method.Parameters.Select((parameter, index) => "(" + TypeName(parameter.Type) + ")arguments[" + index + "]!"));
        var call = target + "." + method.Name + "(" + arguments + ")";
        var returnType = method.ReturnType;
        var fullReturn = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var builder = new StringBuilder();
        if (method.ReturnsVoid)
        {
            builder.Append("    private static global::System.Threading.Tasks.ValueTask<object?> ").Append(name)
                .Append("(object? instance, object?[] arguments) { ").Append(call)
                .Append("; return new global::System.Threading.Tasks.ValueTask<object?>((object?)null); }");
        }
        else if (fullReturn == "global::System.Threading.Tasks.Task")
        {
            builder.Append("    private static async global::System.Threading.Tasks.ValueTask<object?> ").Append(name)
                .Append("(object? instance, object?[] arguments) { await ").Append(call)
                .Append(".ConfigureAwait(false); return null; }");
        }
        else if (fullReturn == "global::System.Threading.Tasks.ValueTask")
        {
            builder.Append("    private static async global::System.Threading.Tasks.ValueTask<object?> ").Append(name)
                .Append("(object? instance, object?[] arguments) { await ").Append(call)
                .Append(".ConfigureAwait(false); return null; }");
        }
        else if (returnType is INamedTypeSymbol named && named.IsGenericType &&
                 (named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.Tasks.Task<TResult>" ||
                  named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.Tasks.ValueTask<TResult>"))
        {
            builder.Append("    private static async global::System.Threading.Tasks.ValueTask<object?> ").Append(name)
                .Append("(object? instance, object?[] arguments) { return (object?)await ").Append(call)
                .Append(".ConfigureAwait(false); }");
        }
        else
        {
            builder.Append("    private static global::System.Threading.Tasks.ValueTask<object?> ").Append(name)
                .Append("(object? instance, object?[] arguments) { var result = ").Append(call)
                .Append("; return new global::System.Threading.Tasks.ValueTask<object?>((object?)result); }");
        }
        return builder.ToString();
    }

    private static string BuildOverheadWrapper(IMethodSymbol method, string name)
    {
        var returnType = method.ReturnType;
        var fullReturn = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (method.ReturnsVoid)
            return "    private static global::System.Threading.Tasks.ValueTask<object?> " + name + "(object? instance, object?[] arguments) => new global::System.Threading.Tasks.ValueTask<object?>((object?)null);";
        if (fullReturn == "global::System.Threading.Tasks.Task")
            return "    private static async global::System.Threading.Tasks.ValueTask<object?> " + name + "(object? instance, object?[] arguments) { await global::System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false); return null; }";
        if (fullReturn == "global::System.Threading.Tasks.ValueTask")
            return "    private static async global::System.Threading.Tasks.ValueTask<object?> " + name + "(object? instance, object?[] arguments) { await global::System.Threading.Tasks.ValueTask.CompletedTask.ConfigureAwait(false); return null; }";
        if (returnType is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length == 1)
        {
            var resultType = TypeName(named.TypeArguments[0]);
            if (named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.Tasks.Task<TResult>")
                return "    private static async global::System.Threading.Tasks.ValueTask<object?> " + name + "(object? instance, object?[] arguments) { return (object?)await global::System.Threading.Tasks.Task.FromResult(default(" + resultType + ")).ConfigureAwait(false); }";
            if (named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.Tasks.ValueTask<TResult>")
                return "    private static async global::System.Threading.Tasks.ValueTask<object?> " + name + "(object? instance, object?[] arguments) { return (object?)await global::System.Threading.Tasks.ValueTask.FromResult(default(" + resultType + ")).ConfigureAwait(false); }";
        }
        return "    private static global::System.Threading.Tasks.ValueTask<object?> " + name + "(object? instance, object?[] arguments) => new global::System.Threading.Tasks.ValueTask<object?>((object?)default(" + TypeName(returnType) + "));";
    }

    private static List<Dictionary<string, ConstantValue>> BuildParameterSets(INamedTypeSymbol type)
    {
        var parameters = new List<KeyValuePair<string, ConstantValue[]>>();
        foreach (var member in type.GetMembers())
        {
            var attribute = member.GetAttributes().FirstOrDefault(static item => item.AttributeClass?.Name == "ParamsAttribute" && item.AttributeClass.ContainingNamespace.ToDisplayString() == AttributeNamespace);
            if (attribute is null)
                continue;
            parameters.Add(new KeyValuePair<string, ConstantValue[]>(member.Name, GetArrayArgument(attribute, 0).Select(static value => new ConstantValue(ConstantExpression(value), value.Value?.ToString() ?? "null")).ToArray()));
        }
        var sets = new List<Dictionary<string, ConstantValue>> { new() };
        foreach (var parameter in parameters)
        {
            var expanded = new List<Dictionary<string, ConstantValue>>();
            foreach (var set in sets)
            foreach (var value in parameter.Value)
            {
                var clone = new Dictionary<string, ConstantValue>(set) { [parameter.Key] = value };
                expanded.Add(clone);
            }
            sets = expanded;
        }
        return sets;
    }

    private static ImmutableArray<TypedConstant> GetLegacyArguments(AttributeData attribute, bool benchmark, out string expected, out bool hasExpected, out string? name)
    {
        var typeName = attribute.AttributeClass?.Name ?? string.Empty;
        name = GetLegacyName(attribute);
        var arrayIndex = typeName.IndexOf('N') >= 0 ? 1 : 0;
        var values = GetArrayArgument(attribute, arrayIndex);
        hasExpected = typeName.Contains("R");
        if (hasExpected && values.Length > 0)
        {
            expected = ConstantExpression(values[values.Length - 1]);
            return values.RemoveAt(values.Length - 1);
        }
        expected = "null";
        return values;
    }

    private static string? GetLegacyName(AttributeData attribute)
    {
        var name = attribute.AttributeClass?.Name ?? string.Empty;
        return name.IndexOf('N') >= 0 && attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string : null;
    }

    private static ImmutableArray<TypedConstant> GetArrayArgument(AttributeData attribute, int index)
    {
        if (index < attribute.ConstructorArguments.Length && attribute.ConstructorArguments[index].Kind == TypedConstantKind.Array)
            return attribute.ConstructorArguments[index].Values;
        return ImmutableArray<TypedConstant>.Empty;
    }

    private static AttributeData? FindAttribute(ISymbol symbol, string name) => symbol.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.Name == name && attribute.AttributeClass.ContainingNamespace.ToDisplayString() == AttributeNamespace);
    private static IEnumerable<AttributeData> FindAttributes(ISymbol symbol, string name) => symbol.GetAttributes().Where(attribute => attribute.AttributeClass?.Name == name && attribute.AttributeClass.ContainingNamespace.ToDisplayString() == AttributeNamespace);

    private static bool Inherits(INamedTypeSymbol? type, string baseName)
    {
        while (type is not null)
        {
            if (type.Name == baseName && type.ContainingNamespace.ToDisplayString() == AttributeNamespace)
                return true;
            type = type.BaseType;
        }
        return false;
    }

    private static string[] GetCategories(IMethodSymbol method) => method.ContainingType.GetAttributes().Concat(method.GetAttributes())
        .Where(static attribute => attribute.AttributeClass?.Name == "CategoryAttribute")
        .Select(static attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
        .Where(static value => value is not null).Cast<string>().Distinct().ToArray();

    private static string TraitDictionary(IMethodSymbol method)
    {
        var values = method.ContainingType.GetAttributes().Concat(method.GetAttributes())
            .Where(static attribute => attribute.AttributeClass?.Name == "TraitAttribute" && attribute.ConstructorArguments.Length == 2)
            .Select(static attribute => new { Name = attribute.ConstructorArguments[0].Value as string, Value = attribute.ConstructorArguments[1].Value as string })
            .Where(static pair => pair.Name is not null && pair.Value is not null)
            .Select(pair => "[" + Escape(pair.Name!) + "] = " + Escape(pair.Value!));
        return "new global::System.Collections.Generic.Dictionary<string, string> { " + string.Join(", ", values) + " }";
    }

    private static bool HasInheritedAttribute(IMethodSymbol method, string name) => FindAttribute(method, name) is not null || FindAttribute(method.ContainingType, name) is not null;
    private static string? GetInheritedString(IMethodSymbol method, string name) => GetConstructorString(FindAttribute(method, name) ?? FindAttribute(method.ContainingType, name));
    private static string? GetTypeString(INamedTypeSymbol type, string name) => GetConstructorString(FindAttribute(type, name));
    private static string? GetConstructorString(AttributeData? attribute) => attribute is not null && attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string : null;
    private static int GetInt(IMethodSymbol method, string name) => GetConstructorInt(FindAttribute(method, name) ?? FindAttribute(method.ContainingType, name));
    private static int GetConstructorInt(AttributeData? attribute) => attribute is not null && attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is int value ? value : 0;
    private static string? GetNamedString(AttributeData? attribute, string name) => attribute?.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value as string;
    private static bool GetNamedBool(AttributeData? attribute, string name) => attribute?.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value as bool? ?? false;
    private static int GetNamedInt(AttributeData? attribute, string name) => attribute?.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value is int value ? value : 0;

    private static string ArrayExpression(IEnumerable<TypedConstant> values) => "new object?[] { " + string.Join(", ", values.Select(ConstantExpression)) + " }";
    private static string StringArray(IEnumerable<string> values) => "new string[] { " + string.Join(", ", values.Select(Escape)) + " }";
    private static string NullableString(string? value) => value is null ? "null" : Escape(value);
    private static string Escape(string value) => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, true);
    private static string TypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    private static string Id(IMethodSymbol method, int index) => method.ContainingType.ToDisplayString() + "." + method.Name + "#" + index;

    private static string ConstantExpression(TypedConstant value)
    {
        if (value.IsNull)
            return "null";
        if (value.Kind == TypedConstantKind.Type && value.Value is ITypeSymbol type)
            return "typeof(" + TypeName(type) + ")";
        if (value.Kind == TypedConstantKind.Enum)
            return "(" + TypeName(value.Type!) + ")" + Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture);
        if (value.Kind == TypedConstantKind.Array)
            return ArrayExpression(value.Values);
        if (value.Value is string text)
            return Escape(text);
        if (value.Value is char character)
            return Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(character, true);
        if (value.Value is bool boolean)
            return boolean ? "true" : "false";
        if (value.Value is float)
            return Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture) + "F";
        if (value.Value is decimal)
            return Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture) + "M";
        if (value.Value is long)
            return Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture) + "L";
        if (value.Value is ulong)
            return Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture) + "UL";
        return Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "null";
    }

    private sealed class ConstantValue
    {
        public ConstantValue(string expression, string displayValue)
        {
            Expression = expression;
            DisplayValue = displayValue;
        }

        public string Expression { get; }
        public string DisplayValue { get; }
    }
}
