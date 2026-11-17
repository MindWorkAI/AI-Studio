//
// Shows the right-click menu of the webview only where it helps: in text fields, where it offers
// pasting and spelling suggestions, and on selected text, where it offers copying. Elsewhere, its
// entries make no sense in the app, such as going back, reloading, printing, or opening a link in
// a new window. On a link, the menu never shows, not even within a selection.
//
// The runtime adds this script to every frame of the window, but only in release builds:
// development builds keep the menu, since it opens the developer tools. See runtime/AGENTS.md.
//
(() => {
    const TEXT_INPUT_TYPES = ['text', 'search', 'email', 'url', 'tel', 'password', 'number'];

    const isTextField = element =>
        element instanceof HTMLTextAreaElement
        || (element instanceof HTMLInputElement && TEXT_INPUT_TYPES.includes(element.type))
        || (element instanceof HTMLElement && element.isContentEditable);

    const isOnSelection = event => {
        const selection = window.getSelection();
        if (!selection || selection.isCollapsed || selection.toString().trim() === '')
            return false;

        for (let index = 0; index < selection.rangeCount; index++)
            for (const rect of selection.getRangeAt(index).getClientRects())
                if (event.clientX >= rect.left && event.clientX <= rect.right && event.clientY >= rect.top && event.clientY <= rect.bottom)
                    return true;

        return false;
    };

    window.addEventListener('contextmenu', event => {
        const path = event.composedPath();
        if (path.some(node => node instanceof HTMLAnchorElement && node.hasAttribute('href'))) {
            event.preventDefault();
            return;
        }

        const target = path.find(node => node instanceof Element);
        if (target && isTextField(target))
            return;

        if (isOnSelection(event))
            return;

        event.preventDefault();
    }, true);
})();