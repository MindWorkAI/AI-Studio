using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how files in the folder for prompt overrides replace the prompts of the BeyondFacts Lab.
/// </summary>
/// <remarks>
/// The folder serves whoever develops the prompts further. A file there replaces the prompt of the same name only;
/// what the folder lacks, or what it holds besides, changes nothing.
/// </remarks>
[TestFixture]
public sealed class BflPromptOverridesTests
{
    private string directory = string.Empty;

    [SetUp]
    public void CreateDirectory()
    {
        this.directory = Path.Combine(Path.GetTempPath(), $"ai-studio-beyondfacts-lab-prompts-{Guid.NewGuid():N}");
    }

    [TearDown]
    public void DeleteDirectory()
    {
        if (Directory.Exists(this.directory))
            Directory.Delete(this.directory, true);
    }

    [Test]
    public void TheFolderMirrorsTheFilesOfTheLab()
    {
        var path = BflPromptOverrides.FilePath(this.directory, BflPromptLibrary.StepPath(BflStep.KNOWLEDGE));

        Assert.That(path, Is.EqualTo(Path.Combine(this.directory, "Prompts", "steps", $"{BflStep.KNOWLEDGE.FolderName}.md")));
    }

    [Test]
    public void EveryPromptHasAFileOfItsOwnInTheFolder()
    {
        var files = BflPromptLibrary.AllPaths.Select(path => BflPromptOverrides.FilePath(this.directory, path)).ToList();

        Assert.That(files, Is.Unique);
    }

    [Test]
    public async Task AFileInTheFolderReplacesThePromptOfTheSameNameOnly()
    {
        var replaced = BflPromptLibrary.StepPath(BflStep.GOALS_AND_ROLE);
        await this.WriteAsync(replaced, "Ein neuer Prompt für die Ziele.");

        var parts = await BflPromptLibrary.LoadPartsAsync(BflStep.GOALS_AND_ROLE, AddressForm.FORMAL, this.directory);
        var otherStep = await BflPromptLibrary.ReadAsync(BflPromptLibrary.StepPath(BflStep.VALUES_AND_PERSPECTIVES), this.directory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(parts.Step, Is.EqualTo("Ein neuer Prompt für die Ziele."));
            Assert.That(parts.Core, Is.EqualTo(await BflPromptLibrary.ReadBuiltInAsync(BflPromptLibrary.CORE_PATH)));
            Assert.That(otherStep, Is.EqualTo(await BflPromptLibrary.ReadBuiltInAsync(BflPromptLibrary.StepPath(BflStep.VALUES_AND_PERSPECTIVES))));
            Assert.That(await BflPromptOverrides.FindAsync(this.directory), Is.EqualTo(new[] { replaced }));
        }
    }

    [Test]
    public async Task WithoutTheSettingTheFilesReplaceNothing()
    {
        await this.WriteAsync(BflPromptLibrary.CORE_PATH, "Ein neuer Kern.");
        var settings = new DataBeyondFactsLab { AllowPromptOverrides = false };

        var activeDirectory = BflPromptOverrides.ActiveDirectory(settings, this.directory);
        var core = await BflPromptLibrary.ReadAsync(BflPromptLibrary.CORE_PATH, activeDirectory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(activeDirectory, Is.Null);
            Assert.That(core, Is.EqualTo(await BflPromptLibrary.ReadBuiltInAsync(BflPromptLibrary.CORE_PATH)));
            Assert.That(BflPromptOverrides.ActiveDirectory(new DataBeyondFactsLab { AllowPromptOverrides = true }, this.directory), Is.EqualTo(this.directory));
        }
    }

    [Test]
    public async Task AMissingFolderReplacesNothing()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await BflPromptLibrary.ReadAsync(BflPromptLibrary.ONBOARDING_PATH, this.directory), Is.EqualTo(await BflPromptLibrary.ReadBuiltInAsync(BflPromptLibrary.ONBOARDING_PATH)));
            Assert.That(await BflPromptOverrides.FindAsync(this.directory), Is.Empty);
        }
    }

    [Test]
    public async Task AnEmptyFileReplacesNothing()
    {
        await this.WriteAsync(BflPromptLibrary.ONBOARDING_PATH, "  \n");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await BflPromptLibrary.ReadAsync(BflPromptLibrary.ONBOARDING_PATH, this.directory), Is.EqualTo(await BflPromptLibrary.ReadBuiltInAsync(BflPromptLibrary.ONBOARDING_PATH)));
            Assert.That(await BflPromptOverrides.FindAsync(this.directory), Is.Empty);
        }
    }

    [Test]
    public async Task AnUnknownFileChangesNothing()
    {
        Directory.CreateDirectory(Path.Combine(this.directory, "Prompts", "steps"));
        await File.WriteAllTextAsync(Path.Combine(this.directory, "Prompts", "steps", "99-unknown.md"), "Ein Schritt, den es nicht gibt.");
        await File.WriteAllTextAsync(Path.Combine(this.directory, "core.md"), "Ein Kern am falschen Ort.");
        await File.WriteAllTextAsync(Path.Combine(this.directory, "notes.txt"), "Notizen von {{NIEMANDEM}}.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await BflPromptLibrary.ReadAsync(BflPromptLibrary.CORE_PATH, this.directory), Is.EqualTo(await BflPromptLibrary.ReadBuiltInAsync(BflPromptLibrary.CORE_PATH)));
            Assert.That(await BflPromptOverrides.FindAsync(this.directory), Is.Empty);
        }
    }

    [Test]
    public async Task AFileWithAPlaceholderTheLabDoesNotFillThereIsAnError()
    {
        await this.WriteAsync(BflPromptLibrary.StepPath(BflStep.STRATEGY), "Beachte {{DETAILS}}.");
        await this.WriteAsync(BflPromptLibrary.CORE_PATH, "{{STEP}} {{UNKNOWN}}");
        await this.WriteAsync(BflPromptLibrary.ONBOARDING_PATH, "{{RESPONSE_LANGUAGE}} {{KNOWLEDGE}}");

        var inStep = await Assert.ThrowsAsync<BflPromptOverrideException>(async () => await BflPromptLibrary.ReadAsync(BflPromptLibrary.StepPath(BflStep.STRATEGY), this.directory));
        var inCore = await Assert.ThrowsAsync<BflPromptOverrideException>(async () => await BflPromptLibrary.LoadPartsAsync(BflStep.FRAMEWORK, AddressForm.FORMAL, this.directory));
        var inOnboarding = await Assert.ThrowsAsync<BflPromptOverrideException>(async () => await BflPromptLibrary.LoadOnboardingPartsAsync(AddressForm.FORMAL, this.directory));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(inStep!.FileName, Is.EqualTo($"Prompts/steps/{BflStep.STRATEGY.FolderName}.md"));
            Assert.That(inStep.Placeholder, Is.EqualTo(BflPromptTemplate.DETAILS));
            Assert.That(inCore!.FileName, Is.EqualTo("Prompts/core.md"));
            Assert.That(inCore.Placeholder, Is.EqualTo("UNKNOWN"));
            Assert.That(inOnboarding!.FileName, Is.EqualTo("Prompts/onboarding.md"));
            Assert.That(inOnboarding.Placeholder, Is.EqualTo(BflPromptTemplate.KNOWLEDGE));
        }
    }

    [Test]
    public async Task AFileWithThePlaceholdersOfItsPromptGivesAPrompt()
    {
        await this.WriteAsync(BflPromptLibrary.CORE_PATH, "Ein neuer Kern.\n{{RESPONSE_LANGUAGE}}\n{{ADDRESS_FORM}}\n{{KNOWLEDGE}}\n{{STEP}}\n{{MATERIALS}}\n{{DETAILS}}\n{{MISSING_DETAILS}}");
        await this.WriteAsync(BflPromptLibrary.ONBOARDING_PATH, "Ein neues Onboarding.\n{{RESPONSE_LANGUAGE}}\n{{ADDRESS_FORM}}");

        var prompt = BflPromptComposer.Compose(await BflPromptLibrary.LoadPartsAsync(BflStep.TEST_SITUATION, AddressForm.FORMAL, this.directory), BflStep.TEST_SITUATION, new Dictionary<BflStep, BflStepFormData>(), [], "Deutsch");
        var onboarding = BflPromptComposer.ComposeOnboarding(await BflPromptLibrary.LoadOnboardingPartsAsync(AddressForm.FORMAL, this.directory), "Deutsch");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.StartWith("Ein neuer Kern."));
            Assert.That(prompt, Does.Not.Contain("{{"));
            Assert.That(onboarding, Does.StartWith("Ein neues Onboarding."));
            Assert.That(onboarding, Does.Not.Contain("{{"));
        }
    }

    [Test]
    public async Task CopyingGivesTheFolderEveryPromptOfThisVersion()
    {
        var copied = await BflPromptOverrides.CopyBuiltInAsync(this.directory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copied, Is.EqualTo(BflPromptLibrary.AllPaths.Count));
            foreach (var path in BflPromptLibrary.AllPaths)
                Assert.That(await File.ReadAllTextAsync(BflPromptOverrides.FilePath(this.directory, path)), Is.EqualTo(await BflPromptLibrary.ReadBuiltInAsync(path)), path);

            Assert.That(await BflPromptOverrides.FindAsync(this.directory), Is.Empty, "A copy which nobody changed replaces nothing.");
            Assert.That(Directory.EnumerateFiles(this.directory, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }
    }

    [Test]
    public async Task CopyingKeepsEveryFileWhichExists()
    {
        await this.WriteAsync(BflPromptLibrary.CORE_PATH, "Ein neuer Kern.");
        await this.WriteAsync(BflPromptLibrary.StepPath(BflStep.RESULTS), string.Empty);

        var copied = await BflPromptOverrides.CopyBuiltInAsync(this.directory);
        var copiedAgain = await BflPromptOverrides.CopyBuiltInAsync(this.directory);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copied, Is.EqualTo(BflPromptLibrary.AllPaths.Count - 2));
            Assert.That(copiedAgain, Is.Zero);
            Assert.That(await File.ReadAllTextAsync(BflPromptOverrides.FilePath(this.directory, BflPromptLibrary.CORE_PATH)), Is.EqualTo("Ein neuer Kern."));
            Assert.That(await File.ReadAllTextAsync(BflPromptOverrides.FilePath(this.directory, BflPromptLibrary.StepPath(BflStep.RESULTS))), Is.Empty, "Even an empty file is the person's own.");
        }
    }

    [Test]
    public async Task ACopyWithOtherLineBreaksReplacesNothing()
    {
        var path = BflPromptLibrary.StepPath(BflStep.FRAMEWORK);
        var builtIn = await BflPromptLibrary.ReadBuiltInAsync(path);
        await this.WriteAsync(path, builtIn.ReplaceLineEndings("\r\n") + "\r\n");

        Assert.That(await BflPromptOverrides.FindAsync(this.directory), Is.Empty);
    }

    /// <summary>
    /// Writes the file which replaces a prompt.
    /// </summary>
    /// <param name="path">The prompt, one of BflPromptLibrary.AllPaths.</param>
    /// <param name="text">The text of the file.</param>
    private async Task WriteAsync(string path, string text)
    {
        var file = BflPromptOverrides.FilePath(this.directory, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, text);
    }
}