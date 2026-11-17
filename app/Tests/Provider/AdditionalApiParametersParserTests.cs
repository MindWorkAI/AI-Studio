using AIStudio.Provider;

namespace AIStudio.Tests.Provider;

/// <summary>
/// Checks how the additional API parameters of a provider are read.
/// </summary>
/// <remarks>
/// The dialog for providers refuses a parameter named twice, but a configuration plugin brings its
/// providers past the dialog. Read anyway, such parameters would leave it open which of the values
/// reaches the provider, and they used to break every request with an exception nobody caught.
/// </remarks>
[TestFixture]
public sealed class AdditionalApiParametersParserTests
{
    [Test]
    public void TheParametersAreRead()
    {
        var isRead = AdditionalApiParametersParser.TryParse("""
            "temperature": 0.5,
            "reasoning": { "effort": "low" }
            """, out var parameters, out var errorMessage);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isRead, Is.True);
            Assert.That(errorMessage, Is.Null);
            Assert.That(parameters["temperature"], Is.EqualTo(0.5));
            Assert.That(parameters["reasoning"], Is.EqualTo(new Dictionary<string, object> { ["effort"] = "low" }));
        }
    }

    [TestCase("\"temperature\": 0.5, \"temperature\": 0.7", "temperature", TestName = "A parameter named twice")]
    [TestCase("\"reasoning\": { \"effort\": \"low\", \"effort\": \"high\" }", "effort", TestName = "A field named twice inside a parameter")]
    public void AParameterNamedTwiceIsRefused(string additionalParameters, string repeatedName)
    {
        var isRead = AdditionalApiParametersParser.TryParse(additionalParameters, out var parameters, out var errorMessage);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isRead, Is.False);
            Assert.That(parameters, Is.Empty);
            Assert.That(errorMessage, Does.Contain($"'{repeatedName}'"), "The log names the parameter to correct.");
        }
    }
}