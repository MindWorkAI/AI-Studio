//
// Keeps the links of the app inside its window and hands every other link to the runtime. The
// runtime adds this script to the main frame of the window, where it runs before any script of the
// page; see "Only the runtime lets links leave the window" in runtime/AGENTS.md.
//
// This script acts on a click which would open a link somewhere else: a click while holding Shift,
// Ctrl, Cmd, or Alt, a click with another mouse button than the main one, or a click on a link with
// a target such as _blank.
//
// On a link of the app, such a click does nothing. A browser outside the window cannot show the
// app anyway, since it lacks the session cookie, and the webview would load the whole app anew.
//
// On any other link, the click goes on to the webview, which asks the runtime for a new window or
// a navigation. The runtime opens the link in the system browser instead, on Linux through the
// desktop portal. No script of the page sees such a click: the link script of the shell plugin
// would open every link with a target itself, links of the app included, and through an xdg-open
// which fails in an AppImage.
//
// Dragging a link of the app does nothing either, since dropping it onto a browser would open it
// there.
//
(() => {
    const TARGETS_OF_THIS_WINDOW = ['', '_self', '_top', '_parent'];

    const findLink = event =>
        event.composedPath().find(node => node instanceof HTMLAnchorElement && node.hasAttribute('href')) ?? null;

    // Where a link leads: 'app', 'outside', or null for an address the webview cannot read.
    const destinationOf = link => {
        try {
            return new URL(link.href, location.href).origin === location.origin ? 'app' : 'outside';
        } catch {
            return null;
        }
    };

    const opensElsewhere = (event, link) =>
        event.button !== 0
        || event.shiftKey
        || event.ctrlKey
        || event.metaKey
        || event.altKey
        || !TARGETS_OF_THIS_WINDOW.includes(link.target.toLowerCase());

    const handleClick = event => {
        const link = findLink(event);
        if (!link || !opensElsewhere(event, link))
            return;

        switch (destinationOf(link)) {
            case 'app':
                event.preventDefault();
                event.stopImmediatePropagation();
                break;

            case 'outside':
                event.stopImmediatePropagation();
                break;
        }
    };

    window.addEventListener('click', handleClick, true);
    window.addEventListener('auxclick', handleClick, true);
    window.addEventListener('dragstart', event => {
        const link = findLink(event);
        if (link && destinationOf(link) === 'app')
            event.preventDefault();
    }, true);
})();