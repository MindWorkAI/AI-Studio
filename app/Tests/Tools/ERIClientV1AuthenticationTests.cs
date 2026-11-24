using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient;
using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.Services;
using AIStudio.Tools.Validation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that the ERI client sends no username, password, or access token which an HTTP header
/// cannot carry, and what it says instead.
/// </summary>
/// <remarks>
/// The data source points to port 1 of the own machine, where nothing listens: a request which the
/// client sends fails right away, with a message of its own. A message about the credentials thus
/// shows that the client stopped before sending. Nothing here asserts on the wording of a message:
/// I18N is process-wide state without a reset.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ERIClientV1AuthenticationTests
{
    private const string NAME = "Intranet";

    // A part of each secret which no message could contain by chance, whatever its language:
    private const string SECRET_PART = "Xq7";

    private static readonly string ZERO_WIDTH_SPACE = ((char)0x200B).ToString();

    private static readonly DataSourceERI_V1 UNREACHABLE = new()
    {
        Id = "44444444-4444-4444-4444-444444444444",
        Name = NAME,
        Type = DataSourceType.ERI_V1,
        Hostname = "http://127.0.0.1",
        Port = 1,
        AuthMethod = AuthMethod.TOKEN,
        SecurityPolicy = DataSourceSecurity.ALLOW_ANY,
        MaxMatches = 10,
    };

    private RustService rustService = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;

    [SetUp]
    public void PrepareServices()
    {
        this.rustService = new RustService("1", "unused");
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, this.rustService);

        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection()
            .AddSingleton(this.rustService)
            .AddSingleton(settingsManager)
            .AddLogging()
            .BuildServiceProvider();

        Program.SERVICE_PROVIDER = this.serviceProvider;
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
        this.rustService.Dispose();
    }

    private async Task<APIResponse<AuthResponse>> AuthenticateAsync(DataSourceERI_V1 dataSource, string secret)
    {
        using var client = new ERIClientV1(dataSource);
        return await client.AuthenticateAsync(this.rustService, secret);
    }

    [Test]
    public async Task ATokenWithAnInvisibleCharacterIsNotSent()
    {
        var response = await this.AuthenticateAsync(UNREACHABLE, $"tok{ZERO_WIDTH_SPACE}en-{SECRET_PART}");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Successful, Is.False);
            Assert.That(response.Message, Is.EqualTo(ERICredentialValidation.GetUnusableSecretMessage(AuthMethod.TOKEN, NAME)));
            Assert.That(response.Message, Does.Not.Contain(SECRET_PART));
        }
    }

    [Test]
    public async Task APasswordWithAnUmlautIsNotSent()
    {
        var dataSource = UNREACHABLE with { AuthMethod = AuthMethod.USERNAME_PASSWORD, Username = "max" };
        var response = await this.AuthenticateAsync(dataSource, $"Grüße 2024 {SECRET_PART}");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Successful, Is.False);
            Assert.That(response.Message, Is.EqualTo(ERICredentialValidation.GetUnusableSecretMessage(AuthMethod.USERNAME_PASSWORD, NAME)));
            Assert.That(response.Message, Does.Not.Contain(SECRET_PART));
        }
    }

    [Test]
    public async Task AUsernameWithAnUmlautIsNotSent()
    {
        var dataSource = UNREACHABLE with { AuthMethod = AuthMethod.USERNAME_PASSWORD, Username = "jürgen" };
        var response = await this.AuthenticateAsync(dataSource, "Mein Passwort");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Successful, Is.False);
            Assert.That(response.Message, Is.EqualTo(ERICredentialValidation.GetUnusableUsernameMessage(NAME)));
        }
    }

    [Test]
    public async Task APasswordWithSpacesIsSent()
    {
        var dataSource = UNREACHABLE with { AuthMethod = AuthMethod.USERNAME_PASSWORD, Username = "Max Mustermann" };
        var response = await this.AuthenticateAsync(dataSource, " Mein Passwort\n");
        string[] messagesAboutCredentials =
        [
            ERICredentialValidation.GetUnusableSecretMessage(AuthMethod.USERNAME_PASSWORD, NAME),
            ERICredentialValidation.GetUnusableUsernameMessage(NAME),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Successful, Is.False, "Nothing listens on port 1.");
            Assert.That(messagesAboutCredentials, Has.No.Member(response.Message), "The client has to send the request.");
        }
    }

    [Test]
    public async Task ATokenOfPrintableAsciiIsSent()
    {
        var response = await this.AuthenticateAsync(UNREACHABLE, "eyJhbGciOiJIUzI1NiJ9.e30.abc-_~+/=\n");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Successful, Is.False, "Nothing listens on port 1.");
            Assert.That(response.Message, Is.Not.EqualTo(ERICredentialValidation.GetUnusableSecretMessage(AuthMethod.TOKEN, NAME)), "The client has to send the request.");
        }
    }
}