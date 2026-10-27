using AIStudio.Components;
using AIStudio.Tools;

namespace AIStudio.Tests.Components;

public sealed class ManagePandocDependencyTests
{
    [Test]
    public void TheInstallationPromptStaysHiddenWhileAvailabilityIsChecked()
    {
        Assert.That(ManagePandocDependency.ShouldShowInstallationPrompt(null), Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AnAvailableInstallationNeedsNoPrompt(bool isLocal)
    {
        var installation = new PandocInstallation(true, string.Empty, true, "3.7.0.2", isLocal);

        Assert.That(ManagePandocDependency.ShouldShowInstallationPrompt(installation), Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AnUnavailableOrOutdatedInstallationShowsThePrompt(bool checkWasSuccessful)
    {
        var installation = new PandocInstallation(checkWasSuccessful, "Unavailable", false, string.Empty, false);

        Assert.That(ManagePandocDependency.ShouldShowInstallationPrompt(installation), Is.True);
    }
}
