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
using Microsoft.CodeAnalysis.Formatting;

namespace SourceCodeRules.UsageCodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyStringCodeFixProvider)), Shared]
public class EmptyStringCodeFixProvider : CodeFixProvider
{
    private const string TITLE = """Replace "" with string.Empty""";
    
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [Identifier.EMPTY_STRING_ANALYZER];
    
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
    
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if(root is null)
            return;
        
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        
        if (root.FindToken(diagnosticSpan.Start).Parent is not LiteralExpressionSyntax emptyStringLiteral)
            return;
        
        context.RegisterCodeFix(
            CodeAction.Create(
                title: TITLE,
                createChangedDocument: c => ReplaceWithStringEmpty(context.Document, emptyStringLiteral, c),
                equivalenceKey: TITLE),
            diagnostic);
    }
    private static async Task<Document> ReplaceWithStringEmpty(Document document, LiteralExpressionSyntax emptyStringLiteral, CancellationToken cancellationToken)
    {
        var stringEmptyExpression = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("string"), SyntaxFactory.IdentifierName("Empty")).WithAdditionalAnnotations(Formatter.Annotation);
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (root is null)
            return document;
        
        var newRoot = root.ReplaceNode(emptyStringLiteral, stringEmptyExpression);
        return document.WithSyntaxRoot(newRoot);
    }
}