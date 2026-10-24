using AIStudio.Components;
using AIStudio.Tools;

namespace AIStudio.Tests.Components;

/// <summary>
/// Checks which actions a path gets: those of a file, those of a web page, or none.
/// </summary>
/// <remarks>
/// A button which cannot work must not show: a relative path would open another file depending on
/// where it is opened from, and the address of a mail names it for AI Studio only.
/// </remarks>
[TestFixture]
public sealed class FileActionsTests
{
    [Test]
    public void AnAbsolutePathIsAFile() => Assert.That(FileActions.DetermineKind(Path.Combine(Path.GetTempPath(), "minutes.pdf")), Is.EqualTo(FileActions.FileActionsKind.FILE));

    [TestCase("https://example.org/budget-2026")]
    [TestCase("http://intranet.example.org/wiki/page")]
    public void AWebAddressIsAWebPage(string address) => Assert.That(FileActions.DetermineKind(address), Is.EqualTo(FileActions.FileActionsKind.WEB_PAGE));

    [TestCase("")]
    [TestCase("notes/minutes.txt")]
    public void ARelativePathOrNothingGetsNoActions(string path) => Assert.That(FileActions.DetermineKind(path), Is.EqualTo(FileActions.FileActionsKind.NONE));

    [Test]
    public void AMailGetsNoActions() => Assert.That(FileActions.DetermineKind(SourceExtensions.CreateMailSourceUrl("6a1f2b3c-4d5e-4f60-8a7b-9c0d1e2f3a4b", "mail-1")), Is.EqualTo(FileActions.FileActionsKind.NONE));
}