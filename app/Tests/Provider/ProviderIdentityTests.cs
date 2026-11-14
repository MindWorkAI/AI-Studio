using AIStudio.Provider;

namespace AIStudio.Tests.Provider;

/// <summary>
/// Checks how messages to the user name a provider which hides which provider it is, see IProvider.HidesIdentity.
/// </summary>
[TestFixture]
public sealed class ProviderIdentityTests
{
    private static readonly Model MODEL = new("gpt-test", null);

    [Test]
    public void AProviderNamesItselfByDefault()
    {
        var provider = new NoProvider { InstanceName = "My provider" };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(provider.NameForUser, Is.EqualTo("My provider"));
            Assert.That(provider.TypeForUser, Is.EqualTo(nameof(LLMProviders.NONE)));
            Assert.That(provider.ModelNameForUser(MODEL), Is.EqualTo("gpt-test"));
        }
    }

    [Test]
    public void AProviderWhichHidesItsIdentityNamesNothing()
    {
        var provider = new NoProvider { InstanceName = "My provider", HidesIdentity = true };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(provider.NameForUser, Does.Not.Contain("My provider"));
            Assert.That(provider.TypeForUser, Does.Not.Contain(nameof(LLMProviders.NONE)));
            Assert.That(provider.ModelNameForUser(MODEL), Does.Not.Contain("gpt-test"));
        }
    }
}