using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that every TB and TA wrapper of the app looks its texts up under the class it belongs to.
/// </summary>
/// <remarks>
/// The I18N collector files the texts of a TB or TA wrapper under the namespace and the class of its source file,
/// and the language plugins translate them under that key. A wrapper which names another class in its typeof and
/// nameof looks every text up under a key no plugin knows, so its texts stay English in every language, and
/// nothing reports it. The wrapper of the Assistant Builder's parse errors named the enum instead of its own
/// class, which left twelve messages untranslated.
/// </remarks>
[TestFixture]
public sealed class TranslationWrapperTests
{
    [Test]
    public void EveryTBWrapperNamesItsOwnClass()
    {
        var mismatches = FindMismatches("TB", out var wrappers);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(wrappers, Is.GreaterThan(100), "The test found too few TB wrappers to be looking at the source of the app.");
            Assert.That(mismatches, Is.Empty);
        }
    }

    /// <remarks>
    /// The TA of a component, such as that of MSGComponentBase, may name no class at all: it looks its texts up
    /// under the class of the component. A static TA has nothing but the class it names.
    /// </remarks>
    [Test]
    public void EveryTAWrapperNamesItsOwnClass()
    {
        var mismatches = FindMismatches("TA", out var wrappers);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(wrappers, Is.GreaterThan(3), "The test found too few TA wrappers to be looking at the source of the app.");
            Assert.That(mismatches, Is.Empty);
        }
    }

    /// <summary>
    /// Finds the wrappers of the given name which name another class than their own, or a static one which names none.
    /// </summary>
    private static List<string> FindMismatches(string wrapperName, out int wrappers)
    {
        var appDirectory = AppSourceDirectory();
        var mismatches = new List<string>();
        var files = Directory.EnumerateFiles(appDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(Path.GetRelativePath(appDirectory, path)));

        wrappers = 0;
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            if (!source.Contains($"{wrapperName}(", StringComparison.Ordinal))
                continue;

            var root = CSharpSyntaxTree.ParseText(source).GetRoot();
            foreach (var wrapper in root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(method => method.Identifier.Text == wrapperName))
            {
                wrappers++;
                var owner = wrapper.Ancestors().OfType<BaseTypeDeclarationSyntax>().Last().Identifier.Text;
                var named = NamedTypes(wrapper).ToList();
                var mayNameNone = wrapperName == "TA" && wrapper.Modifiers.All(modifier => modifier.Text != "static");
                if ((named.Count == 0 && !mayNameNone) || named.Any(name => name != owner))
                    mismatches.Add($"{Path.GetRelativePath(appDirectory, file)}: the {wrapperName} of {owner} names {(named.Count == 0 ? "no class" : string.Join(", ", named))}");
            }
        }

        return mismatches;
    }

    /// <summary>
    /// The types a wrapper names to build its key, through typeof and nameof.
    /// </summary>
    private static IEnumerable<string> NamedTypes(MethodDeclarationSyntax wrapper)
    {
        foreach (var typeOf in wrapper.DescendantNodes().OfType<TypeOfExpressionSyntax>())
            yield return SimpleName(typeOf.Type);

        foreach (var invocation in wrapper.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not IdentifierNameSyntax { Identifier.Text: "nameof" })
                continue;

            switch (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression)
            {
                case MemberAccessExpressionSyntax memberAccess:
                    yield return memberAccess.Name.Identifier.Text;
                    break;

                case TypeSyntax type:
                    yield return SimpleName(type);
                    break;
            }
        }
    }

    private static string SimpleName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => SimpleName(qualified.Right),
        SimpleNameSyntax simple => simple.Identifier.Text,
        _ => type.ToString(),
    };

    private static bool IsBuildOutput(string relativePath)
    {
        var firstDirectory = relativePath.Split(Path.DirectorySeparatorChar)[0];
        return firstDirectory is "bin" or "obj";
    }

    private static string AppSourceDirectory([CallerFilePath] string sourceFilePath = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "..", "MindWork AI Studio"));
}
