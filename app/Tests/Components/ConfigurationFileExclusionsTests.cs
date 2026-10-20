using AIStudio.Components;

namespace AIStudio.Tests.Components;

public sealed class ConfigurationFileExclusionsTests
{
    [Test]
    public async Task AParentRefreshDoesNotReplaceAnUncommittedDraft()
    {
        var component = new TestableConfigurationFileExclusions();
        try
        {
            await component.RefreshAsync();
            component.Edit("*.new");
            await component.RefreshAsync();

            Assert.That(component.Draft, Is.EqualTo("*.new"));
        }
        finally
        {
            component.ReleaseResources();
        }
    }

    [Test]
    public async Task AnExternalSettingsChangeReplacesTheDraft()
    {
        var component = new TestableConfigurationFileExclusions();
        try
        {
            await component.RefreshAsync();
            component.Edit("*.new");
            component.StoredText = "*.external";
            await component.RefreshAsync();

            Assert.That(component.Draft, Is.EqualTo("*.external"));
        }
        finally
        {
            component.ReleaseResources();
        }
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task DisabledOrLockedFieldsRejectEdits(bool disabled, bool locked)
    {
        var component = new TestableConfigurationFileExclusions();
        try
        {
            await component.RefreshAsync();
            component.DisableField = disabled;
            component.LockField = locked;
            component.Edit("*.new");

            Assert.That(component.Draft, Is.EqualTo("*.original"));
        }
        finally
        {
            component.ReleaseResources();
        }
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task BecomingDisabledOrLockedBeforeAutosaveRejectsThePendingWrite(bool disabled, bool locked)
    {
        var component = new TestableConfigurationFileExclusions();
        try
        {
            await component.RefreshAsync();
            component.Edit("*.new");
            component.DisableField = disabled;
            component.LockField = locked;
            await component.FlushAsync();

            Assert.That(component.StoredText, Is.EqualTo("*.original"));
        }
        finally
        {
            component.ReleaseResources();
        }
    }

    [Test]
    public async Task LockingAFieldReplacesItsDraftWithTheStoredSetting()
    {
        var component = new TestableConfigurationFileExclusions();
        try
        {
            await component.RefreshAsync();
            component.Edit("*.new");
            component.LockField = true;
            await component.RefreshAsync();

            Assert.That(component.Draft, Is.EqualTo("*.original"));
        }
        finally
        {
            component.ReleaseResources();
        }
    }

    [Test]
    public async Task InvalidRulesAreNotPersisted()
    {
        var component = new TestableConfigurationFileExclusions();
        try
        {
            await component.RefreshAsync();
            component.RejectEdits();
            component.Edit(":text:");
            await component.FlushAsync();

            Assert.That(component.StoredText, Is.EqualTo("*.original"));
        }
        finally
        {
            component.ReleaseResources();
        }
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task DisabledOrLockedFieldsRejectExplicitReplacement(bool disabled, bool locked)
    {
        var component = new TestableConfigurationFileExclusions();
        try
        {
            await component.RefreshAsync();
            component.DisableField = disabled;
            component.LockField = locked;
            await component.ReplaceAsync("bin/;*.tmp");

            Assert.Multiple(() =>
            {
                Assert.That(component.Draft, Is.EqualTo("*.original"));
                Assert.That(component.StoredText, Is.EqualTo("*.original"));
            });
        }
        finally
        {
            component.ReleaseResources();
        }
    }

    [Test]
    public async Task ExplicitReplacementKeepsAnInvalidOtherGroupsDraftWithoutPersistingIt()
    {
        var component = new TestableConfigurationFileExclusions();
        try
        {
            await component.RefreshAsync();
            component.RejectEdits();
            component.Edit("old/;:text:");
            await component.ReplaceAsync("bin/;:text:");

            Assert.Multiple(() =>
            {
                Assert.That(component.Draft, Is.EqualTo("bin/;:text:"));
                Assert.That(component.StoredText, Is.EqualTo("*.original"));
            });
        }
        finally
        {
            component.ReleaseResources();
        }
    }

    private sealed class TestableConfigurationFileExclusions : ConfigurationFileExclusions
    {
        public string StoredText { get; set; } = "*.original";

        public bool DisableField { get; set; }

        public bool LockField { get; set; }

        public string Draft => this.EditingText;

        public TestableConfigurationFileExclusions()
        {
            this.Text = () => this.StoredText;
            this.TextUpdate = text => this.StoredText = text;
            this.Disabled = () => this.DisableField;
            this.IsLocked = () => this.LockField;
        }

        public void Edit(string text) => this.InternalUpdate(text);

        public void RejectEdits() => this.Validation = _ => "Invalid rule";

        public Task RefreshAsync() => this.OnParametersSetAsync();

        public Task FlushAsync() => this.OptionChanged(this.EditingText);

        public Task ReplaceAsync(string text) => this.ApplyTextAsync(text);

        // These state-only tests never register with a message bus or render the component.
        public void ReleaseResources() => this.DisposeResources();
    }
}
