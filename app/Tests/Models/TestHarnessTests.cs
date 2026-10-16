using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tests.Models;

/// <summary>
/// Checks the test harness itself, before any test states something about the app.
/// </summary>
/// <remarks>
/// Three things have to hold before a capability test can mean anything: the app assembly is
/// referenced, this project counts as a friend assembly, and the assembly-wide setup has run. When
/// one of them is missing, the failure looks like a broken rule rather than a broken harness, which
/// is an expensive detour. These two tests make the difference visible right away.
///
/// Note the fully written type name below. AIStudio.Provider is a namespace and AIStudio.Settings
/// .Provider is a type; inside a namespace under AIStudio, the namespace wins the lookup. That is a
/// property of the app's own naming, not of the tests.
/// </remarks>
[TestFixture]
public sealed class TestHarnessTests
{
    [Test]
    public void TheStaticApplicationStateIsAvailable()
    {
        //
        // Settings.Provider initializes a static logger from Program.LOGGER_FACTORY. Touching it
        // without the assembly-wide setup throws a TypeInitializationException.
        //
        Assert.That(AIStudio.Settings.Provider.NONE.UsedLLMProvider, Is.EqualTo(LLMProviders.NONE));
    }

    [Test]
    public void TheCapabilityApiOfTheAppIsReachable()
    {
        var profile = LLMProviders.OPEN_AI.GetModelProfile(new Model("gpt-5.1", null));

        Assert.That(profile.Has(Capability.FUNCTION_CALLING), Is.True);
    }
}