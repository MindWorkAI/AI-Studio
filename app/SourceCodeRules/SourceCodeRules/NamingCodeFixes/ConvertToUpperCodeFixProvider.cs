using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace SourceCodeRules.NamingCodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConvertToUpperCodeFixProvider)), Shared]
public sealed class ConvertToUpperCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => [Identifier.CONST_STATIC_ANALYZER, Identifier.LOCAL_CONSTANTS_ANALYZER];
    
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
    
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var declaration = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<VariableDeclaratorSyntax>().First();
        if (declaration is null)
            return;
        
        context.RegisterCodeFix(CodeAction.Create(title: "Convert to UPPER_CASE", createChangedDocument: c => this.ConvertToUpperCaseAsync(context.Document, declaration, c), equivalenceKey: nameof(ConvertToUpperCodeFixProvider)), diagnostic);
    }
    
    private async Task<Document> ConvertToUpperCaseAsync(Document document, VariableDeclaratorSyntax declarator, CancellationToken cancellationToken)
    {
        var oldName = declarator.Identifier.Text;
        var newName = ConvertToUpperCase(oldName);
        
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        var symbol = semanticModel?.GetDeclaredSymbol(declarator, cancellationToken);
        if (symbol is null)
            return document;

        var solution = document.Project.Solution;
        var newSolution = await Renamer.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), newName, cancellationToken);
        
        return newSolution.GetDocument(document.Id) ?? document;
    }
    
    private static string ConvertToUpperCase(string name)
    {
        var result = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            
            // Insert an underscore before each uppercase letter, except the first one:
            if (i > 0 && char.IsUpper(current) && !char.IsUpper(name[i - 1]))
                result.Append('_');
            
            result.Append(char.ToUpper(current));
        }
        
        return result.ToString();
    }
}