using AIStudio.Tools;
using AIStudio.Tools.CodeHighlighting;

namespace AIStudio.Tests.Tools.CodeHighlighting;

/// <summary>
/// Checks that every kind of part takes its color from the style sheet of MudBlazor.Markdown, which
/// colors the code blocks with the palette of the theme.
/// </summary>
[TestFixture]
public sealed class CodeTokenKindExtensionsTests
{
    [Test]
    public async Task EveryKindHasAClassWhichTheStyleSheetColors([Values] CodeTokenKind kind)
    {
        var styleSheet = await new WebRootFiles().ReadTextAsync("system/MudBlazor.Markdown/MudBlazor.Markdown.min.css");
        var cssClass = kind.ToCssClass().Split(' ')[0];

        Assert.That(styleSheet, Does.Contain($".{cssClass}"), "A library which renames its classes would leave this kind without color.");
    }
}