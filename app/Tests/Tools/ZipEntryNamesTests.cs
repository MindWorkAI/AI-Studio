using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks the names of ZIP entries before anything is extracted.
/// </summary>
/// <remarks>
/// A plugin archive, the configuration of an organization, or a backup comes from somewhere else. A
/// name which leads outside of the target directory would let it overwrite any file the user may
/// write, the settings among them.
/// </remarks>
[TestFixture]
public sealed class ZipEntryNamesTests
{
    [TestCase("plugin/plugin.lua", "plugin/plugin.lua")]
    [TestCase("plugin\\plugin.lua", "plugin/plugin.lua")]
    [TestCase("data/workspaces/", "data/workspaces/")]
    public void ASafeNameIsKeptWithForwardSlashes(string entryName, string expected)
    {
        Assert.That(ZipEntryNames.Normalize(entryName), Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("/etc/passwd")]
    [TestCase("\\Windows\\win.ini")]
    [TestCase("C:/Users/settings.json")]
    [TestCase("../settings.json")]
    [TestCase("data/../../settings.json")]
    [TestCase("data\\..\\..\\settings.json")]
    [TestCase("./plugin.lua")]
    [TestCase("plugin\0.lua")]
    public void ANameWhichCouldLeadElsewhereIsRejected(string entryName)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => ZipEntryNames.Normalize(entryName), Throws.InstanceOf<InvalidDataException>());
            Assert.That(ZipEntryNames.TryNormalize(entryName, out var normalizedEntryName), Is.False);
            Assert.That(normalizedEntryName, Is.Empty);
        }
    }

    [Test]
    public void ASafeNameLiesBelowTheTargetDirectory()
    {
        var targetDirectory = Path.Combine(Path.GetTempPath(), "ai-studio-zip-target");

        var destinationPath = ZipEntryNames.GetDestinationPath(targetDirectory, ZipEntryNames.Normalize("attachments/transcripts/meeting.md"));

        Assert.That(destinationPath, Is.EqualTo(Path.Combine(Path.GetFullPath(targetDirectory), "attachments", "transcripts", "meeting.md")));
    }
}