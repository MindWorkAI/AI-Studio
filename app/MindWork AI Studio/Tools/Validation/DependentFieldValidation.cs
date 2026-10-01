using MudBlazor.Interfaces;

namespace AIStudio.Tools.Validation;

/// <summary>
/// Gives form fields a fresh verdict when a field they are checked against changes.
/// </summary>
/// <remarks>
/// A MudForm checks a field only when that very field changes. Where the rule of one field reads
/// another one -- the required confidence level is checked against the embedding provider, and so
/// are the token limits -- changing the other field leaves the verdict as it was, and an error which
/// no longer holds stays on screen. Hooked to the FieldChanged event of the form, this checks the
/// dependent fields again after every change. A state which is no field of the form, such as
/// showing the expert settings, passes no changed field at all.
/// </remarks>
public static class DependentFieldValidation
{
    /// <summary>
    /// Validates the dependent fields anew.
    /// </summary>
    /// <remarks>
    /// The field which just changed is skipped, because the form checks it anyway. So is every
    /// field the user has not reached yet and which shows no error: a dialog just opened must not
    /// greet anybody with errors about fields they have not filled in.
    /// </remarks>
    /// <param name="changedField">The field the form reported as changed, or null when the change happened outside the fields.</param>
    /// <param name="dependentFields">The fields whose rules read other fields. A field which is not rendered yet is null.</param>
    public static async Task RevalidateAsync(IFormComponent? changedField, params IFormComponent?[] dependentFields)
    {
        foreach (var field in dependentFields)
        {
            if (field is null || ReferenceEquals(field, changedField))
                continue;

            if (field.Touched || field.HasErrors)
                await field.Validate();
        }
    }
}