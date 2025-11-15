using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace AsyncGuard.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FireAndForgetCodeFixProvider))]
[Shared]
public sealed class FireAndForgetCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(FireAndForgetAnalyzer.DiagnosticId);

    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (node is not InvocationExpressionSyntax invocation)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Avvolgi con FireAndForget()",
                c => ApplyFixAsync(context.Document, invocation, c),
                equivalenceKey: "FireAndForget"),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(Document document, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        var newInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation.WithoutTrailingTrivia(),
                SyntaxFactory.IdentifierName("FireAndForget")),
            SyntaxFactory.ArgumentList());

        var expressionStatement = invocation.FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (expressionStatement?.Expression is AssignmentExpressionSyntax)
        {
            editor.ReplaceNode(expressionStatement, expressionStatement.WithExpression(newInvocation).WithTriviaFrom(expressionStatement));
        }
        else
        {
            editor.ReplaceNode(invocation, newInvocation.WithTriviaFrom(invocation));
        }
        editor.AddUsingIfMissing("AsyncGuard");

        return editor.GetChangedDocument();
    }
}

internal static class DocumentEditorExtensions
{
    public static void AddUsingIfMissing(this DocumentEditor editor, string namespaceName)
    {
        if (editor.OriginalRoot is not CompilationUnitSyntax root)
            return;

        if (root.Usings.Any(u => u.Name.ToString() == namespaceName))
            return;

        var newUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName));
        editor.AddUsing(newUsing);
    }
}
