using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace SourceCodeRules.NamingCodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnderscorePrefixCodeFixProvider)), Shared]
public sealed class UnderscorePrefixCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => [Identifier.UNDERSCORE_PREFIX_ANALYZER];
    
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
    
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var declaration = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<VariableDeclaratorSyntax>().First();
        if (declaration is null)
            return;
        
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Remove underscore prefix", 
                createChangedDocument: c => this.RemoveUnderscorePrefixAsync(context.Document, declaration, c), 
                equivalenceKey: nameof(UnderscorePrefixCodeFixProvider)), 
            diagnostic);
    }
    
    private async Task<Document> RemoveUnderscorePrefixAsync(Document document, VariableDeclaratorSyntax declarator, CancellationToken cancellationToken)
    {
        var oldName = declarator.Identifier.Text;
        var newName = oldName.TrimStart('_');
        
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        var symbol = semanticModel?.GetDeclaredSymbol(declarator, cancellationToken);
        if (symbol is null)
            return document;
        
        var solution = document.Project.Solution;
        var newSolution = await Renamer.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), newName, cancellationToken);
        
        return newSolution.GetDocument(document.Id) ?? document;
    }
}