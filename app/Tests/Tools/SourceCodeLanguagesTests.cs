using AIStudio.Tools;
using AIStudio.Tools.Rust;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which file a code block becomes, judged by the language the model names behind its
/// opening fence.
/// </summary>
[TestFixture]
public sealed class SourceCodeLanguagesTests
{
    [TestCase("python", "Python (.py)")]
    [TestCase("py", "Python (.py)")]
    [TestCase("rust", "Rust (.rs)")]
    [TestCase("rs", "Rust (.rs)")]
    [TestCase("javascript", "JavaScript (.js)")]
    [TestCase("js", "JavaScript (.js)")]
    [TestCase("bash", "Shell (.sh)")]
    [TestCase("sh", "Shell (.sh)")]
    [TestCase("c#", "C# (.cs)")]
    [TestCase("C++", "C++ (.cpp)")]
    [TestCase("Python", "Python (.py)", Description = "Models do not agree on the case.")]
    [TestCase(" yaml ", "YAML (.yaml)", Description = "Space around the name is no part of it.")]
    public void AKnownLanguageNamesItsFile(string language, string expectedName)
    {
        Assert.That(SourceCodeLanguages.FromCodeFenceLanguage(language).ToName(), Is.EqualTo(expectedName));
    }

    [Test]
    public void AnUnknownLanguageBecomesATextFileUnderItsOwnName()
    {
        var language = SourceCodeLanguages.FromCodeFenceLanguage("foo");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(language.Name, Is.EqualTo("foo"), "The user still has to recognize which block it is.");
            Assert.That(language.FileExtension, Is.EqualTo(".txt"));
            Assert.That(language.ToName(), Is.EqualTo("foo (.txt)"));
        }
    }

    [TestCase(null, TestName = "A fence Markdig read no language for")]
    [TestCase("", TestName = "A fence without a language")]
    [TestCase("   ", TestName = "A language made of nothing")]
    [TestCase("text", TestName = "A fence marked as text")]
    [TestCase("PlainText", TestName = "A fence marked as plain text")]
    public void PlainTextBecomesATextFile(string? language)
    {
        Assert.That(SourceCodeLanguages.FromCodeFenceLanguage(language).ToName(), Is.EqualTo("Text (.txt)"));
    }

    [Test]
    public void AFileOfALanguageIsNamedAfterWhatItIsAbout()
    {
        var python = SourceCodeLanguages.FromCodeFenceLanguage("python");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(python.ToSuggestedFileName("Sort: a list?"), Is.EqualTo("Sort a list.py"));
            Assert.That(python.ToSuggestedFileName(), Is.EqualTo("export.py"));
            Assert.That(python.ToFileExtensionOrName(), Is.EqualTo(".py"));
            Assert.That(python.ToFileTypeFilter().FlattenExtensions(), Is.EqualTo(new[] { "py" }), "The dialog offers the one extension we write, without its dot.");
        }
    }

    [TestCase("dockerfile", "Dockerfile")]
    [TestCase("Makefile", "Makefile")]
    [TestCase("cmake", "CMakeLists.txt")]
    public void AToolWhichLooksForAFixedFileNameGetsIt(string language, string expectedFileName)
    {
        var fixedName = SourceCodeLanguages.FromCodeFenceLanguage(language);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fixedName.ToSuggestedFileName("Backend"), Is.EqualTo(expectedFileName), "The tool finds no file under another name, whatever the heading above the block says.");
            Assert.That(fixedName.ToName(), Is.EqualTo(expectedFileName));
            Assert.That(fixedName.ToFileExtensionOrName(), Is.EqualTo(expectedFileName));
            Assert.That(FileTypes.ToFileDialogFilter(fixedName.ToFileTypeFilter()), Is.Null, "A file name is no extension, so the dialog shows every file.");
        }
    }

    [Test]
    public void OnlyPowerShellNeedsAByteOrderMark()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SourceCodeLanguages.FromCodeFenceLanguage("powershell").ToFileEncoding().GetPreamble(), Is.Not.Empty, "Windows PowerShell 5.1 reads a script without one in the ANSI code page.");
            Assert.That(SourceCodeLanguages.FromCodeFenceLanguage("python").ToFileEncoding().GetPreamble(), Is.Empty, "An interpreter has no use for it, and a shebang line breaks behind one.");
            Assert.That(SourceCodeLanguages.Languages.Where(language => language.NeedsByteOrderMark).Select(language => language.Name), Is.EqualTo(new[] { "PowerShell" }));
        }
    }

    [Test]
    public void OnlyABatchFileNeedsWindowsLineEndings()
    {
        Assert.That(SourceCodeLanguages.Languages.Where(language => language.NeedsWindowsLineEndings).Select(language => language.Name), Is.EqualTo(new[] { "Batch" }), "The command prompt misses jump labels in a file whose lines end with LF alone.");
    }

    [Test]
    public void EveryLanguageHasAFile()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (var language in SourceCodeLanguages.Languages)
            {
                Assert.That(language.Name, Is.Not.Empty);
                Assert.That(language.FileExtension.Length is 0 ^ language.FileName.Length is 0, Is.True, $"{language.Name} needs either an extension or a fixed file name, not both.");
                if (language.FileExtension.Length > 0)
                    Assert.That(language.FileExtension, Does.StartWith(".").And.Length.GreaterThan(1), $"{language.Name} has a broken extension.");
            }
        }
    }

    [Test]
    public void NoAliasIsNamedTwice()
    {
        Assert.That(SourceCodeLanguages.Aliases, Is.Unique.Using((IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase), "Plain text is checked first, so a language with the same alias would never be found.");
    }

    [Test]
    public void NoAliasNamesAFormatOfItsOwn()
    {
        // FileExportFormatExtensions.TryFromCodeFenceLanguage is asked first, so such an entry would never be found:
        Assert.That(SourceCodeLanguages.Aliases.Where(alias => FileExportFormatExtensions.TryFromCodeFenceLanguage(alias, out _)), Is.Empty);
    }
}