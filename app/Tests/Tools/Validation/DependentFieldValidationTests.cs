using AIStudio.Tools.Validation;

using MudBlazor.Interfaces;

namespace AIStudio.Tests.Tools.Validation;

/// <summary>
/// Checks which fields get a fresh verdict when a field they are checked against changes.
/// </summary>
/// <remarks>
/// The data source dialogs check the required confidence level against the embedding provider and
/// the other way around. A form only checks the field which changed, so an error on the other one
/// used to stay on screen after it no longer held.
/// </remarks>
[TestFixture]
public sealed class DependentFieldValidationTests
{
    [Test]
    public async Task AFieldShowingAnErrorIsValidatedAnew()
    {
        var changedField = new FakeFormComponent { Touched = true };
        var dependentField = new FakeFormComponent { Error = true };

        await DependentFieldValidation.RevalidateAsync(changedField, changedField, dependentField);
        Assert.That(dependentField.ValidationCount, Is.EqualTo(1), "The stale error is not checked again.");
    }

    [Test]
    public async Task ATouchedFieldIsValidatedAnew()
    {
        var dependentField = new FakeFormComponent { Touched = true };

        await DependentFieldValidation.RevalidateAsync(new FakeFormComponent(), dependentField);
        Assert.That(dependentField.ValidationCount, Is.EqualTo(1), "A field the user has filled in keeps its old verdict.");
    }

    [Test]
    public async Task AFieldTheUserHasNotReachedIsLeftAlone()
    {
        var dependentField = new FakeFormComponent();

        await DependentFieldValidation.RevalidateAsync(new FakeFormComponent(), dependentField);
        Assert.That(dependentField.ValidationCount, Is.Zero, "A field nobody has filled in yet shows an error.");
    }

    [Test]
    public async Task TheChangedFieldIsLeftToTheForm()
    {
        var changedField = new FakeFormComponent { Touched = true, Error = true };

        await DependentFieldValidation.RevalidateAsync(changedField, changedField);
        Assert.That(changedField.ValidationCount, Is.Zero, "The changed field is validated twice.");
    }

    [Test]
    public async Task AChangeOutsideTheFieldsValidatesEveryReachedField()
    {
        var touchedField = new FakeFormComponent { Touched = true };
        var untouchedField = new FakeFormComponent();

        await DependentFieldValidation.RevalidateAsync(null, touchedField, untouchedField, null);
        Assert.Multiple(() =>
        {
            Assert.That(touchedField.ValidationCount, Is.EqualTo(1), "The touched field is not checked again.");
            Assert.That(untouchedField.ValidationCount, Is.Zero, "The untouched field is checked.");
        });
    }

    private sealed class FakeFormComponent : IFormComponent
    {
        public int ValidationCount { get; private set; }

        public bool Required { get; set; }

        public bool Error { get; set; }

        public bool HasErrors => this.Error;

        public bool Touched { get; init; }

        public object? Validation { get; set; }

        public bool IsForNull => true;

        public List<string> ValidationErrors { get; set; } = [];

        public Task Validate()
        {
            this.ValidationCount++;
            return Task.CompletedTask;
        }

        public Task ResetAsync() => Task.CompletedTask;

        public void ResetValidation()
        {
        }
    }
}