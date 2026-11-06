//
// Keeps the links of the app inside its window. The runtime adds this script to the main frame of
// the window, where it runs before any script of the page; see "Only the runtime lets links leave
// the window" in runtime/AGENTS.md.
//
// A click which would open a link of the app somewhere else does nothing: a click while holding
// Shift, Ctrl, Cmd, or Alt, a click with another mouse button than the main one, or a click on a
// link with a target such as _blank. Left alone, such a click would reach the link script of the
// shell plugin, which opens every link with a target in the system browser, links of the app
// included. Or the webview would load the whole app anew. A browser outside the window cannot show
// the app anyway, since it lacks the session cookie.
//
// Links to anything outside the app pass unchanged. The link script of the shell plugin, the
// handler for new windows, or the handler for navigations sends them to the system browser.
//
// Dragging a link of the app does nothing either, since dropping it onto a browser would open it
// there.
//
(() => {
    const TARGETS_OF_THIS_WINDOW = ['', '_self', '_top', '_parent'];

    const findAppLink = event => {
        const link = event.composedPath().find(node => node instanceof HTMLAnchorElement && node.hasAttribute('href'));
        if (!link)
            return null;

        let url;
        try {
            url = new URL(link.href, location.href);
        } catch {
            return null;
        }

        return url.origin === location.origin ? link : null;
    };

    const opensElsewhere = (event, link) =>
        event.button !== 0
        || event.shiftKey
        || event.ctrlKey
        || event.metaKey
        || event.altKey
        || !TARGETS_OF_THIS_WINDOW.includes(link.target.toLowerCase());

    const keepInWindow = event => {
        const link = findAppLink(event);
        if (!link || !opensElsewhere(event, link))
            return;

        event.preventDefault();
        event.stopImmediatePropagation();
    };

    window.addEventListener('click', keepInWindow, true);
    window.addEventListener('auxclick', keepInWindow, true);
    window.addEventListener('dragstart', event => {
        if (findAppLink(event))
            event.preventDefault();
    }, true);
})();