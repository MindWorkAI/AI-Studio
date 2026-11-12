using AIStudio.Chat;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks that the thinking which open models write into their answer is removed, whichever markers
/// the model uses, so that it never reaches a chat, a result file, or a results table.
/// </summary>
[TestFixture]
public sealed class RemoveThinkTagsTests
{
    [TestCase("<think>Let me see.</think>\n\nThe answer.", TestName = "DeepSeek, Qwen, GLM, Kimi K2, and others")]
    [TestCase("\n\n<think>Let me see.</think>\n\nThe answer.", TestName = "A blank line before the thinking")]
    [TestCase("Let me see.\n</think>\n\nThe answer.", TestName = "Only the closing marker, since Qwen 3.5")]
    [TestCase("[THINK]Let me see.[/THINK]The answer.", TestName = "Magistral")]
    [TestCase("<|START_THINKING|>Let me see.<|END_THINKING|>\nThe answer.", TestName = "Command A of Cohere")]
    [TestCase("◁think▷Let me see.◁/think▷The answer.", TestName = "Older versions of Kimi")]
    [TestCase("<|channel>thought\nLet me see.<channel|>The answer.", TestName = "Gemma 4")]
    [TestCase("<think>First.</think>\n<think>Second.</think>\nThe answer.", TestName = "Several blocks of thinking")]
    [TestCase("<|channel|>analysis<|message|>Let me see.<|end|><|start|>assistant<|channel|>final<|message|>The answer.<|return|>", TestName = "gpt-oss in the Harmony format")]
    public void OnlyTheAnswerRemains(string text)
    {
        Assert.That(text.RemoveThinkTags().Trim(), Is.EqualTo("The answer."));
    }

    [TestCase("<think>Still thinking")]
    [TestCase("  <think>Still thinking")]
    [TestCase("[THINK]Still thinking")]
    [TestCase("<|channel|>analysis<|message|>Still thinking")]
    [TestCase("<|start|>assistant<|channel|>analysis<|message|>Still thinking")]
    public void AModelWhichIsStillThinkingShowsNothing(string text)
    {
        Assert.That(text.RemoveThinkTags(), Is.Empty);
    }

    [TestCase("The answer.")]
    [TestCase("Models write <think> before and </think> after their thinking.", TestName = "An answer which explains the markers")]
    [TestCase("The answer.\n<think>Afterthought.</think>", TestName = "Thinking in the middle of an answer")]
    [TestCase("Here is my thought process: none. Here is my response: the answer.", TestName = "Thinking in plain text, as Granite writes it")]
    [TestCase("gpt-oss starts its answer with <|channel|>final<|message|> and ends it with <|return|>.", TestName = "An answer which quotes the Harmony markers")]
    [TestCase("Magistral ends its thinking with [/THINK], Gemma 4 with <channel|>.", TestName = "An answer which quotes other closing markers")]
    public void AnAnswerWithoutLeadingThinkingStaysAsItIs(string text)
    {
        Assert.That(text.RemoveThinkTags(), Is.EqualTo(text));
    }
}