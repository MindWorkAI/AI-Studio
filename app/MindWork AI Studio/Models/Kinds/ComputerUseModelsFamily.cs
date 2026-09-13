using AIStudio.Provider;

namespace AIStudio.Models.Kinds;

/// <summary>
/// The models that work a screen instead of holding a conversation.
/// </summary>
/// <remarks>
/// They are named after the chat model they grew out of -- gemini-2.5-computer-use-preview -- and a
/// name is all they share with it. A request without the computer use tool is refused outright:
/// "This model requires the use of the Computer Use tool." So the resemblance is exactly the trap,
/// and this is the rule that keeps them out of the list a person picks a chat partner from.
/// </remarks>
public sealed class ComputerUseModelsFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.UNKNOWN;

    /// <inheritdoc />
    public override ModelSource Source => new("https://ai.google.dev/gemini-api/docs/computer-use", new DateOnly(2026, 9, 12), "Found while testing the switch-over: the model stood in the chat list although its API refuses every request which does not carry the computer use tool.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Modifier("computer-use").AsSegment().Kind(ModelKind.COMPUTER_USE);
}