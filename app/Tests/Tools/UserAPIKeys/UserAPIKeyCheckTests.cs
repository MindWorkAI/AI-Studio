using AIStudio.Provider;
using AIStudio.Tools.UserAPIKeys;

namespace AIStudio.Tests.Tools.UserAPIKeys;

/// <summary>
/// Checks what AI Studio reads from the models a provider loads with the API key the user entered.
/// </summary>
/// <remarks>
/// Only a refusal keeps the key out of the OS keyring, and only a list with models counts as accepted. Many
/// providers succeed with an empty list whatever the key, for example for a kind of model they do not offer;
/// telling the user that such a key was accepted would be a promise nobody checked.
/// </remarks>
[TestFixture]
public sealed class UserAPIKeyCheckTests
{
    [TestCase(ModelLoadFailureReason.INVALID_OR_MISSING_API_KEY)]
    [TestCase(ModelLoadFailureReason.AUTHENTICATION_OR_PERMISSION_ERROR)]
    public void ARefusedSignInRejectsTheKey(ModelLoadFailureReason failureReason)
    {
        Assert.That(PendingUserAPIKeys.ClassifyCheck(ModelLoadResult.Failure(failureReason)), Is.EqualTo(UserAPIKeyCheckOutcome.REJECTED));
    }

    [Test]
    public void ModelsLoadedWithTheKeyAcceptIt()
    {
        Assert.That(PendingUserAPIKeys.ClassifyCheck(ModelLoadResult.FromModels([new Model("gpt-6", "GPT-6")])), Is.EqualTo(UserAPIKeyCheckOutcome.ACCEPTED));
    }

    [Test]
    public void AnEmptyListTellsNothingAboutTheKey()
    {
        Assert.That(PendingUserAPIKeys.ClassifyCheck(ModelLoadResult.FromModels([])), Is.EqualTo(UserAPIKeyCheckOutcome.NOT_CHECKED));
    }

    [TestCase(ModelLoadFailureReason.INSUFFICIENT_QUOTA)]
    [TestCase(ModelLoadFailureReason.TOO_MANY_REQUESTS)]
    [TestCase(ModelLoadFailureReason.PROVIDER_UNAVAILABLE)]
    [TestCase(ModelLoadFailureReason.INVALID_RESPONSE)]
    [TestCase(ModelLoadFailureReason.UNKNOWN)]
    public void AnyOtherFailureTellsNothingAboutTheKey(ModelLoadFailureReason failureReason)
    {
        Assert.That(PendingUserAPIKeys.ClassifyCheck(ModelLoadResult.Failure(failureReason)), Is.EqualTo(UserAPIKeyCheckOutcome.NOT_CHECKED));
    }
}