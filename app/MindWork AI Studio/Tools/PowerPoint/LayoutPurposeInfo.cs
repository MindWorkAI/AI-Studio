namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// Everything known about one kind of slide.
/// </summary>
/// <param name="Area">The area the forms list the kind of slide in.</param>
/// <param name="Label">The short name of the choice, e.g., "Layout for chapters", for the user.</param>
/// <param name="Question">The question the choice answers, which says which slides it applies to, for the user.</param>
/// <param name="PromptDescription">What the slides of this kind are, in English, for the model.</param>
internal readonly record struct LayoutPurposeInfo(LayoutPurposeArea Area, string Label, string Question, string PromptDescription);