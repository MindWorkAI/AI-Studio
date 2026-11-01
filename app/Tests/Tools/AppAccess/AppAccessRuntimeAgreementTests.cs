using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AIStudio.Tools.Security;

namespace AIStudio.Tests.Tools.AppAccess;

/// <summary>
/// Checks that the app server and the Rust runtime agree on the names around the session cookie.
/// </summary>
/// <remarks>
/// The runtime sets the cookie and hands the token over in an environment variable; the server
/// reads both under its own constants. Should the names drift apart, the server would refuse the
/// app window, and nothing but a start of the app would tell. The test reads the constants of the
/// runtime from its source.
/// </remarks>
[TestFixture]
public sealed class AppAccessRuntimeAgreementTests
{
    private static string ReadRuntimeSource([CallerFilePath] string sourceFilePath = "") => File.ReadAllText(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "..", "..", "runtime", "src", "app_access.rs"));

    private static string ReadRustConstant(string name)
    {
        var match = Regex.Match(ReadRuntimeSource(), $"""pub const {name}: &str = "([^"]+)";""");
        Assert.That(match.Success, Is.True, $"The runtime defines no constant {name}.");
        return match.Groups[1].Value;
    }

    [Test]
    public void BothUseTheSameCookieName()
    {
        Assert.That(ReadRustConstant("SESSION_COOKIE_NAME"), Is.EqualTo(AppAccessPolicy.SESSION_COOKIE_NAME));
    }

    [Test]
    public void BothUseTheSameEnvironmentVariable()
    {
        Assert.That(ReadRustConstant("DOTNET_ENV_UI_TOKEN"), Is.EqualTo(AppAccessPolicy.UI_TOKEN_ENVIRONMENT_VARIABLE));
    }
}