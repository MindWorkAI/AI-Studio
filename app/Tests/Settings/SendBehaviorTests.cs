using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components.Web;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks which keys send a message, for each shortcut a person can choose.
/// </summary>
[TestFixture]
public sealed class SendBehaviorTests
{
    [TestCase(SendBehavior.ENTER_IS_SENDING, "Enter", false, true)]
    [TestCase(SendBehavior.ENTER_IS_SENDING, "NumpadEnter", false, true)]
    [TestCase(SendBehavior.ENTER_IS_SENDING, "Enter", true, false)]
    [TestCase(SendBehavior.MODIFER_ENTER_IS_SENDING, "Enter", false, false)]
    [TestCase(SendBehavior.MODIFER_ENTER_IS_SENDING, "Enter", true, true)]
    [TestCase(SendBehavior.MODIFER_ENTER_IS_SENDING, "NumpadEnter", true, true)]
    [TestCase(SendBehavior.NO_KEY_IS_SENDING, "Enter", false, false)]
    [TestCase(SendBehavior.NO_KEY_IS_SENDING, "Enter", true, false)]
    public void EnterSendsAsChosen(SendBehavior behavior, string code, bool withShift, bool sends)
    {
        Assert.That(behavior.IsSendShortcut(new KeyboardEventArgs { Code = code, ShiftKey = withShift }), Is.EqualTo(sends));
    }

    [TestCase(SendBehavior.ENTER_IS_SENDING)]
    [TestCase(SendBehavior.MODIFER_ENTER_IS_SENDING)]
    public void OtherKeysNeverSend(SendBehavior behavior)
    {
        Assert.That(behavior.IsSendShortcut(new KeyboardEventArgs { Code = "KeyA" }), Is.False);
        Assert.That(behavior.IsSendShortcut(new KeyboardEventArgs { Code = "KeyA", CtrlKey = true }), Is.False);
    }

    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    public void EveryModifierCounts(bool alt, bool ctrl, bool meta)
    {
        var keyEvent = new KeyboardEventArgs { Code = "Enter", AltKey = alt, CtrlKey = ctrl, MetaKey = meta };

        Assert.That(SendBehavior.MODIFER_ENTER_IS_SENDING.IsSendShortcut(keyEvent), Is.True);
        Assert.That(SendBehavior.ENTER_IS_SENDING.IsSendShortcut(keyEvent), Is.False);
    }
}