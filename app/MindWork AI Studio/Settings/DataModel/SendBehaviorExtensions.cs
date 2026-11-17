using Microsoft.AspNetCore.Components.Web;

namespace AIStudio.Settings.DataModel;

public static class SendBehaviorExtensions
{
    /// <param name="behavior">The shortcut the person chose for sending.</param>
    extension(SendBehavior behavior)
    {
        /// <summary>
        /// Checks whether a key released in a message field sends the message.
        /// </summary>
        /// <remarks>
        /// Every message field of the app reads the keys this way, so that the shortcut chosen for the chat holds
        /// wherever the person writes to a model.
        /// </remarks>
        /// <param name="keyEvent">The key released.</param>
        /// <returns>True when the key sends the message.</returns>
        public bool IsSendShortcut(KeyboardEventArgs keyEvent)
        {
            // Either enter or numpad enter:
            var key = keyEvent.Code.ToLowerInvariant();
            if (key is not ("enter" or "numpadenter"))
                return false;

            var isModifier = keyEvent.AltKey || keyEvent.CtrlKey || keyEvent.MetaKey || keyEvent.ShiftKey;
            return behavior switch
            {
                SendBehavior.ENTER_IS_SENDING => !isModifier,
                SendBehavior.MODIFER_ENTER_IS_SENDING => isModifier,

                _ => false
            };
        }
    }
}