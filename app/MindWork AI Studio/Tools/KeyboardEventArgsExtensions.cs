using Microsoft.AspNetCore.Components.Web;

namespace AIStudio.Tools;

public static class KeyboardEventArgsExtensions
{
    /// <param name="keyEvent">The key pressed.</param>
    extension(KeyboardEventArgs keyEvent)
    {
        /// <summary>
        /// Whether the key is Enter, of the main keyboard or of the number pad, pressed without Alt, Ctrl, or the Command key.
        /// </summary>
        /// <remarks>
        /// Confirms a dialog whose text field takes a single line, e.g., a name. The key and its code are both
        /// asked: the key names what the keyboard layout produces, the code the key on the keyboard. A text
        /// field which takes several lines uses SendBehavior.IsSendShortcut instead, which follows the setting of
        /// the user.
        /// </remarks>
        public bool IsPlainEnter
        {
            get
            {
                var key = keyEvent.Key.ToLowerInvariant();
                var code = keyEvent.Code.ToLowerInvariant();
                if (key is not "enter" && code is not "enter" and not "numpadenter")
                    return false;

                return keyEvent is { AltKey: false, CtrlKey: false, MetaKey: false };
            }
        }
    }
}