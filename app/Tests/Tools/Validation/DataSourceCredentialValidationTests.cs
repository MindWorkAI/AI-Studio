using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.Validation;

namespace AIStudio.Tests.Tools.Validation;

/// <summary>
/// Checks what the dialog of an ERI data source says about the username and the secret entered there.
/// </summary>
/// <remarks>
/// Nothing here asserts on the wording of a message: I18N is process-wide state without a reset.
/// The messages about characters are compared with those of ERICredentialValidation instead.
/// </remarks>
[TestFixture]
public sealed class DataSourceCredentialValidationTests
{
    private const string STORAGE_ISSUE = "The keyring is locked.";

    private static readonly string ZERO_WIDTH_SPACE = ((char)0x200B).ToString();

    private static DataSourceValidation For(AuthMethod authMethod, string secretStorageIssue = "") => new()
    {
        GetAuthMethod = () => authMethod,
        GetSecretStorageIssue = () => secretStorageIssue,
    };

    [TestCase(AuthMethod.NONE)]
    [TestCase(AuthMethod.KERBEROS)]
    public void AnAuthMethodWithoutASecretNeedsNone(AuthMethod authMethod)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(For(authMethod).ValidatingSecret(string.Empty), Is.Null);
            Assert.That(For(authMethod).ValidatingSecret($"a{ZERO_WIDTH_SPACE}b"), Is.Null);
        }
    }

    [TestCase(AuthMethod.TOKEN)]
    [TestCase(AuthMethod.USERNAME_PASSWORD)]
    public void AStorageIssueComesFirst(AuthMethod authMethod)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(For(authMethod, STORAGE_ISSUE).ValidatingSecret($"a{ZERO_WIDTH_SPACE}b"), Is.EqualTo(STORAGE_ISSUE));
            Assert.That(For(authMethod, STORAGE_ISSUE).ValidatingSecret(string.Empty), Is.EqualTo(STORAGE_ISSUE));
        }
    }

    [TestCase(AuthMethod.TOKEN)]
    [TestCase(AuthMethod.USERNAME_PASSWORD)]
    public void AMissingSecretIsReported(AuthMethod authMethod)
    {
        var message = For(authMethod).ValidatingSecret(" ");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message, Is.Not.Null.And.Not.Empty);
            Assert.That(message, Is.Not.EqualTo(ERICredentialValidation.ValidateSecret(authMethod, $"a{ZERO_WIDTH_SPACE}b")), "An empty field is no issue of its characters.");
        }
    }

    [Test]
    public void ATokenWithAnInvisibleCharacterIsReported()
    {
        var token = $"eyJhbGciOiJIUzI1NiJ9{ZERO_WIDTH_SPACE}.e30";
        Assert.That(For(AuthMethod.TOKEN).ValidatingSecret(token), Is.Not.Null.And.EqualTo(ERICredentialValidation.ValidateSecret(AuthMethod.TOKEN, token)));
    }

    [Test]
    public void APasswordWithAnUmlautIsReported() => Assert.That(For(AuthMethod.USERNAME_PASSWORD).ValidatingSecret("Grüße 2024"), Is.Not.Null.And.EqualTo(ERICredentialValidation.ValidateSecret(AuthMethod.USERNAME_PASSWORD, "Grüße 2024")));

    [Test]
    public void APasswordWithSpacesIsAccepted() => Assert.That(For(AuthMethod.USERNAME_PASSWORD).ValidatingSecret("Mein Passwort"), Is.Null);

    [Test]
    public void AUsernameWithAnUmlautIsReported()
    {
        var validation = For(AuthMethod.USERNAME_PASSWORD);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(validation.ValidateUsername("jürgen"), Is.Not.Null.And.EqualTo(ERICredentialValidation.ValidateUsername("jürgen")));
            Assert.That(validation.ValidateUsername("Max Mustermann"), Is.Null);
            Assert.That(validation.ValidateUsername(" "), Is.Not.Null, "An empty username is still reported.");
        }
    }

    [Test]
    public void TheUsernameMattersOnlyForUsernameAndPassword() => Assert.That(For(AuthMethod.TOKEN).ValidateUsername("jürgen"), Is.Null);
}