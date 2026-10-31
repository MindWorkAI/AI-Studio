using AIStudio.Assistants.ERI;

using OperatingSystem = AIStudio.Assistants.ERI.OperatingSystem;

namespace AIStudio.Tests.Assistants.ERI;

/// <summary>
/// Checks that the system prompt of the ERI Server assistant carries every input of the form, in a shape
/// the model can read.
/// </summary>
/// <remarks>
/// Up to v26.10.1, the description of the planned authentication never reached the model. Besides, the
/// heading of the data source ran into the text after it, and all authentication methods stood on a
/// single line.
/// </remarks>
[TestFixture]
public sealed class ERIServerPromptTests
{
    private const string AUTH_DESCRIPTION = "Editors log in with their username and password; the public catalog needs no login.";

    private static readonly string[] HEADINGS =
    [
        "# Introduction",
        "# Data Source",
        "# Authentication and Authorization",
        "# Security",
        "# Retrieval Processes",
        "# Remarks"
    ];

    private static readonly string[] TOKEN_AND_PASSWORD_LINES = ["- Login by static token per user.", "- Login by username and password."];

    [Test]
    public void EveryHeadingStandsOnItsOwnLine([Values] DataSources dataSource)
    {
        var lines = Lines(new ERIServerPrompt { DataSource = dataSource, AuthenticationMethods = [Auth.TOKEN] });

        Assert.That(lines, Is.SupersetOf(HEADINGS));
    }

    [Test]
    public void EachAuthenticationMethodStandsOnItsOwnLine()
    {
        var lines = Lines(new ERIServerPrompt { AuthenticationMethods = [Auth.TOKEN, Auth.USERNAME_PASSWORD] });

        Assert.That(lines, Is.SupersetOf(TOKEN_AND_PASSWORD_LINES));
    }

    [Test]
    public void TheDescriptionOfTheAuthenticationReachesTheModel()
    {
        var lines = Lines(new ERIServerPrompt { AuthenticationMethods = [Auth.NONE, Auth.USERNAME_PASSWORD], AuthDescription = AUTH_DESCRIPTION });

        Assert.Multiple(() =>
        {
            Assert.That(lines, Does.Contain("The authentication process is planned as follows:"));
            Assert.That(lines, Does.Contain(AUTH_DESCRIPTION));
        });
    }

    [Test]
    public void WithoutADescriptionOfTheAuthenticationItsSectionIsLeftOut()
    {
        var prompt = new ERIServerPrompt { AuthenticationMethods = [Auth.TOKEN] }.ToSystemPrompt();

        Assert.That(prompt, Does.Not.Contain("The authentication process is planned as follows:"));
    }

    [Test]
    public void KerberosStillNamesTheOperatingSystemAfterTheDescription()
    {
        var lines = Lines(new ERIServerPrompt { AuthenticationMethods = [Auth.KERBEROS, Auth.TOKEN], AuthDescription = AUTH_DESCRIPTION, OperatingSystem = OperatingSystem.LINUX });

        Assert.That(lines, Does.Contain("The server will run on Linux operating systems. Keep"));
    }

    /// <summary>
    /// The lines of the system prompt, whatever line endings the source of the prompt was checked out with.
    /// </summary>
    private static string[] Lines(ERIServerPrompt prompt) => prompt.ToSystemPrompt().ReplaceLineEndings("\n").Split('\n');
}