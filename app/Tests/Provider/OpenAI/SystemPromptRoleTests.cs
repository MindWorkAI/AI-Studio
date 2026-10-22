using AIStudio.Models;
using AIStudio.Provider;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Tests.Provider.OpenAI;

/// <summary>
/// Checks which role the OpenAI provider sends the system prompt in, model by model.
/// </summary>
/// <remarks>
/// The role used to be guessed from the model's name: whatever began with an "o" or with "gpt-5",
/// or carried a "4o", got "developer", and everything else got "system". GPT-6 fell through that
/// guess and was sent the oldest role. The families state the role now, and these cases ask for it
/// through the same chain the provider uses, so that a family forgetting it shows up here.
/// </remarks>
[TestFixture]
public sealed class SystemPromptRoleTests
{
    [TestCase("gpt-6-luna", "developer")]
    [TestCase("gpt-6-astra", "developer")]
    [TestCase("gpt-5.6", "developer")]
    [TestCase("gpt-4o", "developer")]
    [TestCase("o3", "developer")]
    [TestCase("gpt-4.1", "developer", Description = "No generation rule knows it, so the baseline for new OpenAI models answers.")]
    [TestCase("o1-mini", "user", Description = "An early reasoning model, which takes no system prompt of its own.")]
    [TestCase("o1-mini-2024-09-12", "user")]
    [TestCase("o1-preview", "user")]
    [TestCase("gpt-4", "system")]
    [TestCase("gpt-4-turbo", "system")]
    [TestCase("gpt-3.5-turbo", "system")]
    public void TheSystemPromptGoesInTheRoleTheModelTakes(string modelId, string role)
    {
        var profile = LLMProviders.OPEN_AI.GetModelProfile(new Model(modelId, null));

        Assert.That(profile.SystemPromptRole.ToOpenAIRole(), Is.EqualTo(role));
    }

    [Test]
    public void ARoleNoRuleStatesIsTheOneOpenAIDocumentsToday()
    {
        //
        // A model the rules do not know is almost always a new one, and the older generations which
        // still want "system" say so in their families.
        //
        Assert.That(SystemPromptRole.UNKNOWN.ToOpenAIRole(), Is.EqualTo("developer"));
    }
}