using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Tests.Components;

public sealed class FileExclusionEditorTests
{
    [TestCase(true, "*.pdf")]
    [TestCase(true, "Dockerfile")]
    [TestCase(true, "node_modules")]
    [TestCase(false, "bin/")]
    [TestCase(false, "*.pdf;obj/")]
    [TestCase(true, ":text:")]
    [TestCase(false, ":documents:")]
    [TestCase(true, "src/bin/")]
    [TestCase(false, "**/*.tmp")]
    public async Task WrongFieldAndInvalidRulesAreNotPublished(bool folders, string patterns)
    {
        var published = new List<string>();
        var editor = Create(published);

        await editor.EditAsync(patterns, folders);

        Assert.That(published, Is.Empty);
    }

    [Test]
    public async Task EditingTheOtherFieldCannotPublishAnInvalidDraft()
    {
        var published = new List<string>();
        var editor = Create(published);

        await editor.EditAsync("*.pdf", folders: true);
        await editor.EditAsync("*.bak", folders: false);
        Assert.That(published, Is.Empty);

        await editor.EditAsync("build/", folders: true);
        Assert.That(published, Is.EqualTo(new[] { "build/; *.bak" }));
    }

    [Test]
    public async Task MovingARuleToItsCorrectFieldPublishesItEvenIfTheCombinedTextDoesNotChange()
    {
        var published = new List<string>();
        var editor = Create(published, "bin/");

        await editor.EditAsync("bin/;*.pdf", folders: true);
        await editor.EditAsync("*.pdf", folders: false);
        Assert.That(published, Is.Empty);

        await editor.EditAsync("bin/", folders: true);
        Assert.That(published, Is.EqualTo(new[] { "bin/; *.pdf" }));
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public async Task ResettingOneFieldCannotPersistInvalidRulesInTheOtherField(bool folders, bool explicitReset)
    {
        var published = new List<string>();
        var editor = Create(published);
        var resets = new List<string>();
        if (explicitReset)
            editor.SetParameters(new() { [nameof(FileExclusionEditor.ResetRequested)] = Collect(resets) });

        await editor.EditAsync(folders ? "*.keep;wrong/" : "*.wrong", folders: !folders);
        await editor.ResetAsync(folders);

        Assert.Multiple(() =>
        {
            Assert.That(published, Is.Empty);
            Assert.That(resets, Is.Empty);
        });

        await editor.EditAsync(folders ? "*.keep" : "keep/", folders: !folders);
        Assert.That(published, Is.EqualTo(new[] { folders ? "bin/; *.keep" : "keep/; *.tmp" }));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ResettingTheInvalidFieldRestoresAValidValue(bool folders)
    {
        var published = new List<string>();
        var editor = Create(published);
        var resets = new List<string>();
        editor.SetParameters(new() { [nameof(FileExclusionEditor.ResetRequested)] = Collect(resets) });

        await editor.EditAsync(folders ? "*.wrong" : "wrong/", folders);
        await editor.ResetAsync(folders);

        Assert.Multiple(() =>
        {
            Assert.That(published, Is.Empty);
            Assert.That(resets, Is.EqualTo(new[] { folders ? "bin/; *.custom" : "custom/; *.tmp" }));
        });
    }

    [Test]
    public async Task DisabledEditorRejectsEditsAndBothResetCallbacks()
    {
        var published = new List<string>();
        var resets = new List<string>();
        var editor = Create(published);
        editor.SetParameters(new()
        {
            [nameof(FileExclusionEditor.Disabled)] = true,
            [nameof(FileExclusionEditor.ResetRequested)] = Collect(resets),
        });

        await editor.EditAsync("changed/", folders: true);
        await editor.EditAsync("*.changed", folders: false);
        await editor.ResetAsync(folders: true);
        await editor.ResetAsync(folders: false);

        Assert.Multiple(() =>
        {
            Assert.That(published, Is.Empty);
            Assert.That(resets, Is.Empty);
        });

        editor.SetParameters(new() { [nameof(FileExclusionEditor.ResetRequested)] = default(EventCallback<string>) });
        await editor.ResetAsync(folders: true);
        await editor.ResetAsync(folders: false);
        Assert.That(published, Is.Empty);

        // Returning to editing must reveal the unchanged source, not either attempted edit:
        editor.SetParameters(new() { [nameof(FileExclusionEditor.Disabled)] = false });
        await editor.EditAsync("*.other", folders: false);
        Assert.That(published, Is.EqualTo(new[] { "custom/; *.other" }));
    }

    [Test]
    public async Task AnInvalidDraftMakesTheEditorInvalidWhileTheBoundTextStaysValid()
    {
        var published = new List<string>();
        var editor = Create(published);
        Assert.That(editor.IsValid, Is.True);

        await editor.EditAsync("*.wrong", folders: true);

        Assert.Multiple(() =>
        {
            Assert.That(editor.IsValid, Is.False);
            Assert.That(published, Is.Empty);
        });

        await editor.EditAsync("custom/", folders: true);
        Assert.That(editor.IsValid, Is.True);
    }

    [Test]
    public void AnInvalidReceivedListMakesTheEditorInvalid()
    {
        var editor = Create(new List<string>(), "**/*.tmp");

        Assert.That(editor.IsValid, Is.False);
    }

    [Test]
    public async Task HiddenResetButtonsCannotChangeTheRules()
    {
        var published = new List<string>();
        var editor = Create(published);
        editor.SetParameters(new() { [nameof(FileExclusionEditor.ShowReset)] = false });

        await editor.ResetAsync(folders: true);
        await editor.ResetAsync(folders: false);

        Assert.That(published, Is.Empty);
    }

    private static TestableFileExclusionEditor Create(List<string> published, string initial = "custom/;*.custom")
    {
        var editor = new TestableFileExclusionEditor();
        editor.SetParameters(new()
        {
            [nameof(FileExclusionEditor.Text)] = initial,
            [nameof(FileExclusionEditor.ResetValue)] = "bin/;*.tmp",
            [nameof(FileExclusionEditor.TextChanged)] = Collect(published),
        });
        return editor;
    }

    private static EventCallback<string> Collect(List<string> values) => EventCallback.Factory.Create<string>(new object(), values.Add);

    private sealed class TestableFileExclusionEditor : FileExclusionEditor
    {
        //
        // Sets the parameters the way a renderer would, which this test has none of, and then runs
        // the step of the lifecycle which reacts to them:
        //
        public void SetParameters(Dictionary<string, object?> parameters)
        {
            ParameterView.FromDictionary(parameters).SetParameterProperties(this);
            this.OnParametersSet();
        }

        public Task EditAsync(string patterns, bool folders) => folders ? this.ChangeFoldersAsync(patterns) : this.ChangeFilesAsync(patterns);

        public Task ResetAsync(bool folders) => this.RestoreDefaultsAsync(folders);
    }
}