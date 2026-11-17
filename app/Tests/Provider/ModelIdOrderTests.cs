using AIStudio.Provider;

namespace AIStudio.Tests.Provider;

/// <summary>
/// Checks the order in which the dialogs for providers list the models, newest first.
/// </summary>
/// <remarks>
/// Compared digit by digit, "gpt-4.9" would come before "gpt-4.10" and "o3" before "o10", so an older
/// model would be on top of the list.
/// </remarks>
[TestFixture]
public sealed class ModelIdOrderTests
{
    [TestCase(new[] { "gpt-4.9", "gpt-4.10", "gpt-4.1" }, new[] { "gpt-4.10", "gpt-4.9", "gpt-4.1" }, TestName = "Version numbers after a dot")]
    [TestCase(new[] { "o3", "o10", "o1" }, new[] { "o10", "o3", "o1" }, TestName = "Numbers after a letter")]
    [TestCase(new[] { "llama3.1:8b", "llama3.1:70b" }, new[] { "llama3.1:70b", "llama3.1:8b" }, TestName = "Sizes of a model")]
    [TestCase(new[] { "mistral-large-2407", "mistral-large-2411", "mistral-large-latest" }, new[] { "mistral-large-latest", "mistral-large-2411", "mistral-large-2407" }, TestName = "Dates of a model")]
    public void TheNewestModelsComeFirst(string[] modelIds, string[] expectedOrder)
    {
        var ordered = modelIds.OrderByDescending(id => id, Model.ID_ORDER);

        Assert.That(ordered, Is.EqualTo(expectedOrder));
    }
}