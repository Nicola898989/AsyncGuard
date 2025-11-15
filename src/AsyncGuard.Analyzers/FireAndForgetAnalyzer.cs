using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AsyncGuard.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FireAndForgetAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "AG0001";

    private static readonly LocalizableString Title = "Usa AsyncGuard per i fire-and-forget";
    private static readonly LocalizableString MessageFormat = "Il Task non è awaitato; usare AsyncGuard.FireAndForget()";
    private static readonly LocalizableString Description = "I Task eseguiti senza await devono essere protetti da AsyncGuard per prevenire eccezioni silenziose.";
    private const string Category = "Reliability";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeExpression, SyntaxKind.ExpressionStatement);
    }

    private static void AnalyzeExpression(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ExpressionStatementSyntax expressionStatement)
            return;

        var invocation = ExtractInvocation(expressionStatement.Expression);
        if (invocation is null)
            return;

        if (IsAlreadyFireAndForget(invocation, context))
            return;

        var type = context.SemanticModel.GetTypeInfo(invocation, context.CancellationToken).ConvertedType;
        if (type is null)
            return;

        if (!IsTaskLike(type, context.Compilation))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static InvocationExpressionSyntax? ExtractInvocation(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case InvocationExpressionSyntax invocation:
                return invocation;
            case AssignmentExpressionSyntax assignment when assignment.Right is InvocationExpressionSyntax assignedInvocation:
                if (assignment.Left is IdentifierNameSyntax identifier && identifier.Identifier.Text == "_")
                    return assignedInvocation;
                break;
        }

        return null;
    }

    private static bool IsAlreadyFireAndForget(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var name = memberAccess.Name.Identifier.Text;
            if (name == "FireAndForget")
                return true;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        return symbol?.Name == "FireAndForget";
    }

    private static bool IsTaskLike(ITypeSymbol type, Compilation compilation)
    {
        var task = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        var taskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");

        if (type.Equals(task, SymbolEqualityComparer.Default))
            return true;

        if (type is INamedTypeSymbol named && named.ConstructedFrom?.Equals(taskOfT, SymbolEqualityComparer.Default) == true)
            return true;

        return false;
    }
}
