using AIStudio.Chat;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks how the chat tells the answer of a model apart from the think block which the model may open it with.
/// </summary>
/// <remarks>
/// The chat asks with every chunk of an answer whether there is anything to read yet, to tell the user that the
/// model is thinking until then. It must come to the same result as the text which the chat shows.
/// </remarks>
[TestFixture]
public sealed class ThinkTagsTests
{
    [TestCase("", ExpectedResult = false)]
    [TestCase(" \n", ExpectedResult = false)]
    [TestCase("The answer.", ExpectedResult = true)]
    [TestCase("<think>Let me consider", ExpectedResult = false, Description = "An open think block is all thinking.")]
    [TestCase("<think>Let me consider this.</think>", ExpectedResult = false)]
    [TestCase("<think>Let me consider this.</think>\n\n", ExpectedResult = false, Description = "The line breaks after the block are no answer yet.")]
    [TestCase("<think>Let me consider this.</think>\n\nThe answer.", ExpectedResult = true)]
    public bool TellsWhetherThereIsAnythingToRead(string text) => text.HasTextBeyondThinkTags;

    [TestCase("", ExpectedResult = "")]
    [TestCase("The answer.", ExpectedResult = "The answer.")]
    [TestCase("<think>Let me consider", ExpectedResult = "")]
    [TestCase("<think>Let me consider this.</think>\n\nThe answer.", ExpectedResult = "\n\nThe answer.")]
    public string KeepsOnlyWhatFollowsTheThinkBlock(string text) => text.RemoveThinkTags();

    [Test]
    public void AnAnswerWithoutThinkBlockIsNotCopied()
    {
        const string ANSWER = "The answer.";
        Assert.That(ANSWER.RemoveThinkTags(), Is.SameAs(ANSWER), "The chat removes the think tags with every render of a streamed answer.");
    }
}