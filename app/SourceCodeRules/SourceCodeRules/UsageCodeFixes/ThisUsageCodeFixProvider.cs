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

namespace SourceCodeRules.UsageCodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ThisUsageCodeFixProvider)), Shared]
public class ThisUsageCodeFixProvider : CodeFixProvider
{
    private const string TITLE = "Add 'this.' prefix";
    
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [Identifier.THIS_USAGE_ANALYZER];
    
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
    
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if (root == null)
            return;
        
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var node = root.FindNode(diagnosticSpan);
        
        if (node is IdentifierNameSyntax identifierNode)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: TITLE,
                    createChangedDocument: c => AddThisPrefixAsync(context.Document, identifierNode, c),
                    equivalenceKey: nameof(ThisUsageCodeFixProvider)),
                diagnostic);
        }
        else if (node is GenericNameSyntax genericNameNode)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: TITLE,
                    createChangedDocument: c => AddThisPrefixAsync(context.Document, genericNameNode, c),
                    equivalenceKey: nameof(ThisUsageCodeFixProvider)),
                diagnostic);
        }
    }
    
    private static async Task<Document> AddThisPrefixAsync(Document document, SyntaxNode node, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (root == null)
            return document;
        
        var thisExpression = SyntaxFactory.ThisExpression();
        var leadingTrivia = node.GetLeadingTrivia();
        var memberAccessExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                thisExpression.WithLeadingTrivia(leadingTrivia), 
                ((SimpleNameSyntax)node).WithLeadingTrivia(SyntaxTriviaList.Empty))
            .WithTrailingTrivia(node.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(node, memberAccessExpression);
        return document.WithSyntaxRoot(newRoot);
    }
}