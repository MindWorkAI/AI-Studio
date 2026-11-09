using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The form "Your details" of one step of the BeyondFacts Lab.
/// </summary>
/// <remarks>
/// The form changes the values it gets and tells its parent, which stores them. Empty key details are marked but
/// never block anything.
/// </remarks>
public partial class BflStepForm : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the step whose fields the form shows.
    /// </summary>
    [Parameter, EditorRequired]
    public BflStep Step { get; set; }

    /// <summary>
    /// Gets or sets the values of the form, which the form changes.
    /// </summary>
    [Parameter, EditorRequired]
    public BflStepFormData Form { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the fields cannot be changed.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the callback raised after a value changed.
    /// </summary>
    [Parameter]
    public EventCallback FormChanged { get; set; }

    private IReadOnlyList<BflFieldDefinition> Fields => BflFieldCatalog.ForStep(this.Step);

    private IReadOnlyList<BflFieldDefinition> MissingFields => BflMissingDetails.Of(this.Step, this.Form);

    private bool HasKeyDetails => this.Fields.Any(definition => definition.IsKeyDetail);

    private static string LabelOf(BflFieldDefinition field) => field.IsKeyDetail ? $"{field.Label} *" : field.Label;

    private async Task SetValueAsync(BflFieldDefinition field, string? value)
    {
        var limited = value is not null && value.Length > field.Kind.MaxLength ? value[..field.Kind.MaxLength] : value;
        if (string.Equals(this.Form.Get(field.Id), limited ?? string.Empty, StringComparison.Ordinal))
            return;

        this.Form.Set(field.Id, limited);
        await this.FormChanged.InvokeAsync();
    }

    private async Task SetChoicesAsync(BflFieldDefinition field, IReadOnlyCollection<string>? optionIds)
    {
        this.Form.SetChoices(field, optionIds ?? []);
        await this.FormChanged.InvokeAsync();
    }

    private Func<string, string?>? ValidationOf(BflFieldDefinition field) => field.Kind is BflFieldKind.LINK ? this.ValidateLink : null;

    private string? ValidateLink(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? null
            : TA("Please enter an address that begins with https:// or http://.");
    }
}