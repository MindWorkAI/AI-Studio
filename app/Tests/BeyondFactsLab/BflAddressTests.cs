using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using SharedTools;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks that the texts of the BeyondFacts Lab follow the form of address the person chose.
/// </summary>
/// <remarks>
/// A text which says "you" in English addresses the reader in other languages as well, so it needs TA(). With T() or
/// TB(), it would stay formal in German, although the person chose the informal address.
/// </remarks>
[TestFixture]
public sealed partial class BflAddressTests
{
    [Test]
    public void EveryTextOfTheLabWhichSaysYouFollowsTheFormOfAddress()
    {
        var texts = 0;
        var formalOnly = new List<string>();
        foreach (var file in LabSourceFiles())
        {
            foreach (var text in I18NSourceScanner.FindTexts(File.ReadAllText(file), file, []))
            {
                texts++;
                if (!text.HasAddressVariants && YouRegex().IsMatch(text.Text))
                    formalOnly.Add($"{Path.GetFileName(file)}: {text.Text}");
            }
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(texts, Is.GreaterThan(80), "The test found too few texts to be looking at the lab.");
            Assert.That(formalOnly, Is.Empty);
        }
    }

    [GeneratedRegex(@"\byou(r|rs|rself)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex YouRegex();

    private static IEnumerable<string> LabSourceFiles([CallerFilePath] string sourceFilePath = "")
    {
        var appDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "MindWork AI Studio"));
        return Directory.EnumerateFiles(Path.Combine(appDirectory, "Assistants", "BeyondFactsLab"))
            .Append(Path.Combine(appDirectory, "Dialogs", "Settings", "SettingsDialogBeyondFactsLab.razor"));
    }
}