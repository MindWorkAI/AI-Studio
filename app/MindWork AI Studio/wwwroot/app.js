window.generateDiff = function (text1, text2, divDiff, divLegend) {
    let wikEdDiff = new WikEdDiff();
    let targetDiv = document.getElementById(divDiff)
    if (!targetDiv) {
        return;
    }

    targetDiv.innerHTML = wikEdDiff.diff(text1, text2);
    targetDiv.classList.add('mud-typography-body1', 'improvedDiff');
    
    let legend = document.getElementById(divLegend);
    if (!legend) {
        return;
    }

    legend.innerHTML = `
    <div class="legend mt-2">
        <h3>Legend</h3>
        <ul class="mt-2">
            <li><span class="wikEdDiffMarkRight" title="Moved block" id="wikEdDiffMark999" onmouseover="wikEdDiffBlockHandler(undefined, this, 'mouseover');"></span> Original block position</li>
            <li><span title="+" class="wikEdDiffInsert">Inserted<span class="wikEdDiffSpace"><span class="wikEdDiffSpaceSymbol"></span> </span>text<span class="wikEdDiffNewline"> </span></span></li>
            <li><span title="−" class="wikEdDiffDelete">Deleted<span class="wikEdDiffSpace"><span class="wikEdDiffSpaceSymbol"></span> </span>text<span class="wikEdDiffNewline"> </span></span></li>
            <li><span class="wikEdDiffBlockLeft" title="◀" id="wikEdDiffBlock999" onmouseover="wikEdDiffBlockHandler(undefined, this, 'mouseover');">Moved<span class="wikEdDiffSpace"><span class="wikEdDiffSpaceSymbol"></span> </span>block<span class="wikEdDiffNewline"> </span></span></li>
        </ul>
    </div>
    `;
}

window.clearDiv = function (divName) {
    let targetDiv = document.getElementById(divName);
    if (!targetDiv) {
        return;
    }

    targetDiv.innerHTML = '';
}

// We add a click handler to the provider group headers so that clicking anywhere on the header expands or collapses the group.
// Right now (August 2026), this is not possible using MudBlazor.
document.addEventListener('click', function (event) {
    const target = event.target
    if (!(target instanceof Element))
        return

    const groupHeaderRow = target.closest('tr')
    if (!groupHeaderRow?.querySelector(':scope > .provider-group-header'))
        return

    if (target.closest('.mud-table-row-expander'))
        return

    groupHeaderRow.querySelector('.mud-table-row-expander')?.click()
})

window.scrollToBottom = function(element) {
    element.scrollIntoView({ behavior: 'smooth', block: 'end', inline: 'nearest' });
}

// Scrolls the first element below the container which matches the selector into view, as far as
// needed and no further: an element already in view stays where it is.
window.scrollFirstMatchIntoView = function (container, selector) {
    container?.querySelector(selector)?.scrollIntoView({ block: 'nearest', inline: 'nearest' })
}

// How many frames the settings window waits for the part to come back to, see showSettingsPane: a
// pane may render that part only once its data arrived. About half a second.
const SETTINGS_ANCHOR_FRAMES = 30

// Shows the pane which just took the place of another one in the settings window, see SettingsWindow:
// from its top, where its new scrolling box starts anyway, or, coming back up from a deeper pane, at the
// whole part of it which leads down there. That part carries the path of the deeper pane in
// data-settings-anchor, see SettingsAnchor. It gets the focus, whose frame shows the user where they came
// from until they click elsewhere. Without that part, the pane stays at its top.
window.showSettingsPane = function (options, anchor) {
    if (!options || !anchor)
        return

    let framesLeft = SETTINGS_ANCHOR_FRAMES
    const show = function () {
        const part = [...options.querySelectorAll('[data-settings-anchor]')].find(element => element.dataset.settingsAnchor === anchor)
        if (!part) {
            if (--framesLeft > 0)
                requestAnimationFrame(show)

            return
        }

        part.scrollIntoView({ block: 'center' })
        part.focus({ preventScroll: true })
    }

    show()
}

// The nearest element around the given one which scrolls, and with it the box sticky headers stick to.
const findScrollingBox = function (element) {
    let scrollingBox = element?.parentElement
    while (scrollingBox && !['auto', 'scroll'].includes(getComputedStyle(scrollingBox).overflowY))
        scrollingBox = scrollingBox.parentElement

    return scrollingBox
}

// How far the usual place of a sticky header lies above the line it sticks to at the top, in pixels.
// The usual place is that of the marker right before the header, which never sticks itself. Zero or
// less means the header does not stick to the top. Scrolling the box up by this distance brings the
// header to its usual place, and down by its negative brings a header which lies below there.
const distanceAboveStickyLine = function (scrollingBox, sectionStart) {
    const header = sectionStart.nextElementSibling
    if (!header)
        return 0

    const stickyTop = parseFloat(getComputedStyle(header).top) || 0
    return scrollingBox.getBoundingClientRect().top + stickyTop - sectionStart.getBoundingClientRect().top
}

// Whether a sticky header sticks to the bottom of the box, because its usual place lies below the
// line it sticks to there. Only a header with a bottom of its own does.
const sticksToBottom = function (scrollingBox, sectionStart) {
    const header = sectionStart.nextElementSibling
    const stickyBottom = header ? parseFloat(getComputedStyle(header).bottom) : NaN
    if (Number.isNaN(stickyBottom))
        return false

    const usualBottom = sectionStart.getBoundingClientRect().top + header.offsetHeight
    return usualBottom - (scrollingBox.getBoundingClientRect().bottom - stickyBottom) >= 1
}

// The row of the open chat in the section whose marker is given, the element with the class
// is-current between the header after the marker and the next marker, or null.
const currentRowOfSection = function (sectionStart) {
    for (let element = sectionStart.nextElementSibling?.nextElementSibling; element && !element.classList.contains('workspace-section-start'); element = element.nextElementSibling) {
        const currentRow = element.matches('.is-current') ? element : element.querySelector('.is-current')
        if (currentRow)
            return currentRow
    }

    return null
}

// Measures every section in the scrolling box: its marker, its header, how far the header's usual
// place lies above the line it sticks to at the top, whether it sticks to the top or the bottom, the
// row of the open chat in it, and whether that row is out of sight: above or behind the headers stuck
// to the top, or below or behind those stuck to the bottom.
const measureSections = function (scrollingBox) {
    const sections = [...scrollingBox.querySelectorAll('.workspace-section-start')]
        .map(sectionStart => ({ sectionStart, header: sectionStart.nextElementSibling }))
        .filter(section => section.header)

    for (const section of sections) {
        section.distance = distanceAboveStickyLine(scrollingBox, section.sectionStart)
        section.isStuckToTop = section.distance >= 1
        section.isStuckToBottom = !section.isStuckToTop && sticksToBottom(scrollingBox, section.sectionStart)
        section.isStuck = section.isStuckToTop || section.isStuckToBottom
    }

    // What lies above the lowest header stuck to the top, or below the highest one stuck to the
    // bottom, is out of sight:
    const boxBounds = scrollingBox.getBoundingClientRect()
    const sightTop = Math.max(boxBounds.top, ...sections.filter(section => section.isStuckToTop).map(section => section.header.getBoundingClientRect().bottom))
    const sightBottom = Math.min(boxBounds.bottom, ...sections.filter(section => section.isStuckToBottom).map(section => section.header.getBoundingClientRect().top))
    for (const section of sections) {
        section.currentRow = currentRowOfSection(section.sectionStart)
        const currentRowBounds = section.currentRow?.getBoundingClientRect()
        section.isCurrentRowOutOfSight = !!currentRowBounds && (currentRowBounds.bottom <= sightTop || currentRowBounds.top >= sightBottom)
    }

    return sections
}

// Shows what the header of a section points to, and tells whether it scrolled. When the open chat is
// in the section but out of sight, the list scrolls it into the middle. Otherwise, when the header
// sticks to the top or the bottom of the scrolling box, the list scrolls the start of the section to
// the usual place of the header: back up, or down to a section which still lies below. A header in
// its usual place, whose section shows its open chat or has none, leaves the list where it is, and
// the caller collapses or expands the section instead.
window.revealSection = function (sectionStart) {
    const scrollingBox = findScrollingBox(sectionStart)
    const section = scrollingBox ? measureSections(scrollingBox).find(candidate => candidate.sectionStart === sectionStart) : null
    if (!section)
        return false

    if (section.isCurrentRowOutOfSight) {
        section.currentRow.scrollIntoView({ block: 'center', behavior: 'smooth' })
        return true
    }

    if (!section.isStuck)
        return false

    scrollingBox.scrollBy({ top: -section.distance, behavior: 'smooth' })
    return true
}

// Gives the header after every marker with the class workspace-section-start the attribute
// data-stuck while it sticks to the top or the bottom of the scrolling box around the given list, and
// also data-hides-current while the row of the open chat in its section is out of sight, beyond the
// box or behind the stuck headers. Keeps both current while the box scrolls, changes its size,
// or its content changes, e.g., when another chat opens. CSS cannot tell a header which sticks from
// one in its usual place. Attributes rather than classes, since Blazor would overwrite a class it did
// not render the next time it renders the header. Watching a box a second time changes nothing.
window.watchStuckSections = function (list) {
    const scrollingBox = findScrollingBox(list)
    if (!scrollingBox || scrollingBox.dataset.stuckSectionsWatched)
        return

    scrollingBox.dataset.stuckSectionsWatched = 'true'
    let isUpdateScheduled = false
    const update = function () {
        isUpdateScheduled = false
        for (const section of measureSections(scrollingBox)) {
            section.header.toggleAttribute('data-stuck', section.isStuck)
            section.header.toggleAttribute('data-hides-current', section.isCurrentRowOutOfSight)
        }
    }

    // At most once per frame, however many events the browser sends meanwhile:
    const scheduleUpdate = function () {
        if (isUpdateScheduled)
            return

        isUpdateScheduled = true
        requestAnimationFrame(update)
    }

    scrollingBox.addEventListener('scroll', scheduleUpdate, { passive: true })
    new ResizeObserver(scheduleUpdate).observe(scrollingBox)

    // Opening another chat moves the class is-current, and expanding a section adds rows:
    new MutationObserver(scheduleUpdate).observe(scrollingBox, { subtree: true, childList: true, attributes: true, attributeFilter: ['class'] })
    update()
}

// Lets the user resize the workspace sidebar of the chat page by dragging the handle on its right
// border. The sidebar follows the pointer right away, without a round trip to the server for every
// movement, and its CSS keeps it within its limits, see Chat.razor.css. Once the user lets go, the
// server learns the width the sidebar took and stores it. Watching a handle a second time changes
// nothing.
window.watchSidebarResize = function (handle, sidebar, dotNetReference) {
    if (!handle || !sidebar || handle.dataset.resizeWatched)
        return

    handle.dataset.resizeWatched = 'true'
    let startX = 0
    let startWidth = 0

    const move = function (event) {
        sidebar.style.setProperty('--sidebar-width', `${startWidth + event.clientX - startX}px`)
    }

    const stop = function (event) {
        if (handle.hasPointerCapture(event.pointerId))
            handle.releasePointerCapture(event.pointerId)

        handle.removeEventListener('pointermove', move)
        handle.removeEventListener('pointerup', stop)
        handle.removeEventListener('pointercancel', stop)
        document.documentElement.removeAttribute('data-resizing-sidebar')

        // The pointer may have gone beyond the limits, so the width is the one the sidebar took. The
        // next drag then starts from what the user sees:
        const width = Math.round(sidebar.getBoundingClientRect().width)
        sidebar.style.setProperty('--sidebar-width', `${width}px`)
        dotNetReference.invokeMethodAsync('SidebarResizedAsync', width).catch(() => {})
    }

    handle.addEventListener('pointerdown', function (event) {
        if (event.button !== 0)
            return

        // Keeps the browser from selecting text while the pointer moves:
        event.preventDefault()

        startX = event.clientX
        startWidth = sidebar.getBoundingClientRect().width
        handle.setPointerCapture(event.pointerId)
        document.documentElement.setAttribute('data-resizing-sidebar', '')
        handle.addEventListener('pointermove', move)
        handle.addEventListener('pointerup', stop)
        handle.addEventListener('pointercancel', stop)
    })
}

// Lets the workspace sidebar of the chat page float over the chat. The page decides what the sidebar
// does; this tells the page what the window and the pointer do, and reads from the classes of the
// sidebar whether it shows and whether it floats, see Chat.razor:
// - A window too narrow for the sidebar beside the chat shows it over the chat instead, once the user
//   clicks the toggle above. The page learns whenever the window becomes too narrow or wide enough.
// - While the sidebar is hidden, the pointer resting on the toggle lets it peek over the chat, until
//   the pointer leaves toggle and sidebar. A click on the toggle keeps it.
// - A click outside of the floating sidebar hides it.
// The watch ends by itself once the page is gone. Watching the columns a second time changes nothing.
window.watchChatList = function (columns, dotNetReference) {
    if (!columns || columns.dataset.chatListWatched)
        return

    columns.dataset.chatListWatched = 'true'

    // The pointer rests on the toggle for a moment before the sidebar peeks, since it passes the toggle
    // on its way to the navigation beside it. Leaving toggle and sidebar, it may stray for a moment, as
    // it does on its way from the toggle into the sidebar, before the sidebar hides:
    const PEEK_DELAY_MS = 400
    const LEAVE_DELAY_MS = 300

    let isNarrow = null
    let peekTimer = null
    let leaveTimer = null

    // After a click on the toggle, which showed or hid the sidebar for good, the sidebar peeks no more
    // until the pointer left the toggle. Otherwise, it would come back right after the user hid it:
    let isPeekHeld = false

    const call = function (method, ...args) {
        dotNetReference.invokeMethodAsync(method, ...args).catch(() => {})
    }

    const cancel = function (timer) {
        if (timer !== null)
            clearTimeout(timer)

        return null
    }

    // The least widths of sidebar and chat together, from Chat.razor.css, where both are given in rem:
    const leastWidth = function () {
        const style = getComputedStyle(columns)
        const rem = parseFloat(getComputedStyle(document.documentElement).fontSize)
        return (parseFloat(style.getPropertyValue('--chat-list-least-width')) + parseFloat(style.getPropertyValue('--chat-least-width'))) * rem
    }

    const sidebar = function (classes) {
        return columns.querySelector(`:scope > .chat-sidebar${classes}`)
    }

    const isOnToggle = function (element) {
        return element.closest('.chat-list-toggle') !== null
    }

    // The menus and dialogs which the sidebar opens lie outside of it, but belong to it:
    const isOnSidebar = function (element) {
        return element.closest('.chat-sidebar.is-floating, .mud-popover-open, .mud-overlay, .mud-dialog-container') !== null
    }

    const resizeObserver = new ResizeObserver(function () {
        if (!columns.isConnected) {
            stop()
            return
        }

        const narrow = columns.clientWidth < leastWidth()
        if (narrow === isNarrow)
            return

        isNarrow = narrow
        columns.toggleAttribute('data-narrow', narrow)
        call('SetWindowNarrow', narrow)
    })

    // Follows the pointer onto an element, or out of the window for null:
    const pointTo = function (element) {
        if (element !== null && isOnToggle(element)) {
            leaveTimer = cancel(leaveTimer)
            if (peekTimer === null && !isPeekHeld && sidebar('') === null)
                peekTimer = setTimeout(function () {
                    peekTimer = null
                    call('PeekAtList')
                }, PEEK_DELAY_MS)

            return
        }

        isPeekHeld = false
        peekTimer = cancel(peekTimer)
        if (element !== null && isOnSidebar(element)) {
            leaveTimer = cancel(leaveTimer)
            return
        }

        if (leaveTimer === null && sidebar('.is-peeking') !== null)
            leaveTimer = setTimeout(function () {
                leaveTimer = null
                call('EndListPeek')
            }, LEAVE_DELAY_MS)
    }

    const pointerOver = function (event) {
        if (!columns.isConnected) {
            stop()
            return
        }

        pointTo(event.target instanceof Element ? event.target : null)
    }

    const pointerOut = function (event) {
        if (event.relatedTarget === null && columns.isConnected)
            pointTo(null)
    }

    const pointerDown = function (event) {
        if (!columns.isConnected) {
            stop()
            return
        }

        if (!(event.target instanceof Element))
            return

        if (isOnToggle(event.target)) {
            peekTimer = cancel(peekTimer)
            isPeekHeld = true
            return
        }

        if (sidebar('.is-floating') === null)
            return

        // The bar above belongs to the sidebar as well: its toggle shows and hides the sidebar, and its
        // field searches the sidebar which a click on the toggle showed:
        if (isOnSidebar(event.target) || event.target.closest('.chat-header-bar') !== null)
            return

        peekTimer = cancel(peekTimer)
        leaveTimer = cancel(leaveTimer)
        call('CloseFloatingList')
    }

    const stop = function () {
        resizeObserver.disconnect()
        document.removeEventListener('pointerover', pointerOver)
        document.removeEventListener('pointerout', pointerOut)
        document.removeEventListener('pointerdown', pointerDown, true)
        peekTimer = cancel(peekTimer)
        leaveTimer = cancel(leaveTimer)
    }

    document.addEventListener('pointerover', pointerOver)
    document.addEventListener('pointerout', pointerOut)

    // Before anything else sees the click, which may keep it to itself:
    document.addEventListener('pointerdown', pointerDown, true)
    resizeObserver.observe(columns)
}

// Finding the words of the chat search in the messages of the open chat, and in the message the user is
// writing. The CSS Custom Highlight API paints the matches as ranges, without changing the DOM, which
// Blazor renders and expects unchanged.
// The colors are the ones of the chat search in app.css. One find per messages element; the state
// lives here, and .NET learns the number of matches and which one is the current one.
const chatFindStates = new WeakMap()

const escapeForRegExp = function (text) {
    return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

// Every match of any of the words, ignoring case:
const chatFindPattern = function (terms) {
    return new RegExp(terms.map(escapeForRegExp).join('|'), 'giu')
}

// Every match of any of the words in what the user sees of the messages below the root, as ranges.
// Only the content of a message counts, which is what the search of the sidebar looks at: its text and
// its sources. The header of a message, with its author and time, does not. The text of all text nodes
// counts as one, so a phrase finds its words also across formatting, such as a bold part. Text which
// does not show, such as in a collapsed panel, does not count: stepping to it would show nothing.
const CHAT_MESSAGE_CONTENT = '.mud-card-content'

const findChatMatches = function (root, terms) {
    if (!root || terms.length === 0)
        return []

    const nodes = []
    let text = ''
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT)
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
        const parent = node.parentElement
        if (!node.data || !parent || !parent.closest(CHAT_MESSAGE_CONTENT) || parent.getClientRects().length === 0)
            continue

        nodes.push({ node, start: text.length })
        text += node.data
    }

    // The node which holds the character at the given position, or ends at it for the end of a match:
    const locate = function (position, isEnd) {
        let low = 0
        let high = nodes.length - 1
        while (low < high) {
            const middle = Math.ceil((low + high) / 2)
            const start = nodes[middle].start
            if (start < position || (!isEnd && start === position))
                low = middle
            else
                high = middle - 1
        }

        return [nodes[low].node, position - nodes[low].start]
    }

    const ranges = []
    for (const match of text.matchAll(chatFindPattern(terms))) {
        if (match[0].length === 0)
            continue

        const range = document.createRange()
        const [startNode, startOffset] = locate(match.index, false)
        const [endNode, endOffset] = locate(match.index + match[0].length, true)
        range.setStart(startNode, startOffset)
        range.setEnd(endNode, endOffset)
        ranges.push(range)
    }

    return ranges
}

// Every match of any of the words in the message the user is writing, as positions in the text of its
// field. That message belongs to the chat as much as the sent ones, as for the search of the sidebar,
// and it shows below them. A field cannot show highlights, so its current match shows as the selection
// of the field, see revealChatMatch.
const findFieldMatches = function (fieldId, terms) {
    const field = fieldId ? document.getElementById(fieldId) : null
    if (!field || typeof field.value !== 'string' || terms.length === 0)
        return []

    const matches = []
    for (const match of field.value.matchAll(chatFindPattern(terms)))
        if (match[0].length > 0)
            matches.push({ start: match.index, end: match.index + match[0].length })

    return matches
}

// The matches of the messages come first, then those of the message being written, as the chat shows them:
const chatMatchCount = function (state) {
    return state.ranges.length + state.fieldMatches.length
}

const paintChatMatches = function (state) {
    if (!('highlights' in CSS))
        return

    CSS.highlights.set('chat-search', new Highlight(...state.ranges))
    const current = state.ranges[state.current]
    if (current)
        CSS.highlights.set('chat-search-current', new Highlight(current))
    else
        CSS.highlights.delete('chat-search-current')
}

// Scrolls the current match into the middle of the chat: smoothly when the user stepped to it, at
// once when a chat opened, so that nothing else scrolling at the same time can stop it halfway. A match
// in the message being written gets selected in its field, which always shows below the chat.
const revealChatMatch = function (state, isSmooth) {
    if (state.current >= state.ranges.length) {
        const match = state.fieldMatches[state.current - state.ranges.length]
        const field = document.getElementById(state.fieldId)
        if (match && field) {
            field.focus({ preventScroll: true })
            field.setSelectionRange(match.start, match.end)
        }

        return
    }

    state.ranges[state.current]?.startContainer.parentElement?.scrollIntoView({ block: 'center', behavior: isSmooth ? 'smooth' : 'auto' })
}

// How many matches there are, and which one is the current one, counted from 0, or -1:
const chatFindResult = function (state) {
    return [chatMatchCount(state), state.current]
}

// Finds the words in the messages below the root, and in the message being written in the field with
// the given ID, and marks them. The chat stays where it is, so it does not jump while the user types;
// the arrows go to a match, and a chat which opens shows its last match, see positionOpenedChat. Keeps
// the marks current while the messages change, such as while an answer is written, and while the user
// writes, and tells .NET about the new numbers then.
window.setChatFind = function (root, terms, dotNetReference, fieldId) {
    if (!root)
        return [0, -1]

    let state = chatFindStates.get(root)
    if (!state) {
        state = { terms: [], ranges: [], fieldMatches: [], fieldId: null, current: -1, observer: null, onInput: null, isUpdateScheduled: false }
        chatFindStates.set(root, state)

        const update = function () {
            state.isUpdateScheduled = false
            const [count, current] = chatFindResult(state)
            state.ranges = findChatMatches(root, state.terms)
            state.fieldMatches = findFieldMatches(state.fieldId, state.terms)
            if (state.current >= chatMatchCount(state))
                state.current = chatMatchCount(state) - 1

            paintChatMatches(state)
            const [newCount, newCurrent] = chatFindResult(state)
            if (newCount !== count || newCurrent !== current)
                state.dotNetReference?.invokeMethodAsync('MatchesChangedAsync', newCount, newCurrent).catch(() => {})
        }

        const scheduleUpdate = function () {
            if (state.isUpdateScheduled)
                return

            state.isUpdateScheduled = true
            requestAnimationFrame(update)
        }

        state.observer = new MutationObserver(scheduleUpdate)
        state.observer.observe(root, { subtree: true, childList: true, characterData: true })

        // Typing changes no element, only the value of the field, which the observer does not see. The
        // field may be rendered anew, so the document listens for it:
        state.onInput = function (event) {
            if (state.fieldId && event.target?.id === state.fieldId)
                scheduleUpdate()
        }

        document.addEventListener('input', state.onInput, true)
    }

    state.terms = terms
    state.fieldId = fieldId ?? null
    state.dotNetReference = dotNetReference
    state.ranges = findChatMatches(root, terms)
    state.fieldMatches = findFieldMatches(state.fieldId, terms)
    state.current = -1
    paintChatMatches(state)
    return chatFindResult(state)
}

// Steps to the next match, with 1, or the previous one, with -1, after the last to the first again,
// and scrolls it into view. Without a current match, the next one is the first, the previous one the last.
window.stepChatFind = function (root, direction) {
    const state = root ? chatFindStates.get(root) : null
    if (!state || chatMatchCount(state) === 0)
        return [0, -1]

    const count = chatMatchCount(state)
    state.current = state.current < 0
        ? (direction > 0 ? 0 : count - 1)
        : (state.current + direction + count) % count

    paintChatMatches(state)
    revealChatMatch(state, true)
    return chatFindResult(state)
}

// Takes the marks away and stops watching the messages and the field.
window.clearChatFind = function (root) {
    const state = root ? chatFindStates.get(root) : null
    state?.observer?.disconnect()
    if (state?.onInput)
        document.removeEventListener('input', state.onInput, true)

    if (root)
        chatFindStates.delete(root)

    if ('highlights' in CSS) {
        CSS.highlights.delete('chat-search')
        CSS.highlights.delete('chat-search-current')
    }
}

// How long the messages of a chat which just opened have to stay unchanged before the chat counts as
// shown, in milliseconds, and how long it waits at most. Their Markdown renders after the chat opens,
// and the chat would otherwise scroll to a place which then moves.
const CHAT_SETTLED_AFTER = 150
const CHAT_SETTLED_AT_LATEST = 1500

// Shows where a chat which just opened stands, as soon as its messages are there: at the last match of
// the search when the chat holds one, since a chat has its newest messages at the bottom; otherwise at
// its newest message, if showNewest asks for it. It decides once: a match which shows up later, such
// as in an answer still being written, moves nothing, only the counter of the search. When the user
// scrolls, clicks, or types in the chat before then, the chat stays where the user put it.
window.positionOpenedChat = function (root, showNewest) {
    const scrollingBox = root ? findScrollingBox(root) : null
    if (!scrollingBox)
        return

    const userActions = ['wheel', 'touchstart', 'mousedown', 'keydown']
    let isDone = false
    let isLeftToUser = false
    let settledTimer = 0
    const leaveToUser = function () {
        isLeftToUser = true
    }

    const finish = function () {
        if (isDone)
            return

        isDone = true
        observer.disconnect()
        clearTimeout(settledTimer)
        clearTimeout(latestTimer)
        for (const userAction of userActions)
            scrollingBox.removeEventListener(userAction, leaveToUser)

        if (isLeftToUser)
            return

        // Finds anew rather than trusting the last update of the search, which waits for the next frame:
        const state = chatFindStates.get(root)
        if (state) {
            state.ranges = findChatMatches(root, state.terms)
            state.fieldMatches = findFieldMatches(state.fieldId, state.terms)
        }

        // The last match in the messages, not one in the message being written, which would take the
        // focus into its field:
        if (state && state.ranges.length > 0) {
            state.current = state.ranges.length - 1
            paintChatMatches(state)
            revealChatMatch(state, false)
            const [count, current] = chatFindResult(state)
            state.dotNetReference?.invokeMethodAsync('MatchesChangedAsync', count, current).catch(() => {})
        } else if (showNewest) {
            scrollingBox.scrollTop = scrollingBox.scrollHeight
        }
    }

    const observer = new MutationObserver(function () {
        clearTimeout(settledTimer)
        settledTimer = setTimeout(finish, CHAT_SETTLED_AFTER)
    })

    observer.observe(root, { subtree: true, childList: true, characterData: true })
    for (const userAction of userActions)
        scrollingBox.addEventListener(userAction, leaveToUser, { passive: true })

    settledTimer = setTimeout(finish, CHAT_SETTLED_AFTER)
    const latestTimer = setTimeout(finish, CHAT_SETTLED_AT_LATEST)
}

// Scrolls back to where a sticky header sits in the flow, so that the content below it starts right under the
// header. The anchor is an empty element right before the header: once the page is scrolled past it, the anchor
// lies above the top of its scrolling container, which then moves back by that distance. Otherwise, the content
// below already starts right under the header, and nothing moves.
//
// It sets scrollTop itself: scrollIntoView did not move the page for this anchor without height, and the content
// which renders right after a switch may stop a smooth scroll.
window.scrollToStickyHeader = function(anchor) {
    if (!anchor)
        return;

    // The nearest ancestor which scrolls its content, or else the document:
    let container = anchor.parentElement;
    while (container && !['auto', 'scroll'].includes(getComputedStyle(container).overflowY))
        container = container.parentElement;

    const containerTop = container ? container.getBoundingClientRect().top + container.clientTop : 0;
    container ??= document.scrollingElement;

    const distance = anchor.getBoundingClientRect().top - containerTop;
    if (distance < 0)
        container.scrollTop += distance;
}

// Lets content further down stick right below a sticky header, e.g., a column beside the content. The scrolling
// container of the header gets two CSS variables: --sticky-header-height, the height of the header, and
// --sticky-space-below-header, the height of the container which the header leaves free. Both follow every change
// of size, e.g., when a narrower window wraps the header into more lines.
//
// A header which left the page stops being observed with the next change of size, so nothing needs to stop it.
function updateStickyHeaderVariables(header) {
    const container = header.parentElement;
    if (!header.isConnected || !container)
        return false;

    // The exact height, since offsetHeight rounds to whole pixels: a header rounded down covers the topmost line of
    // the content sticking below it, such as the border of a box.
    const headerHeight = header.getBoundingClientRect().height;
    container.style.setProperty('--sticky-header-height', `${headerHeight}px`);
    container.style.setProperty('--sticky-space-below-header', `${container.clientHeight - headerHeight}px`);
    return true;
}

const stickyHeaderObserver = new ResizeObserver(entries => {
    for (const entry of entries) {
        // The container carries its header along, since it changes size without the header:
        const header = entry.target.stickyHeader ?? entry.target;
        if (!updateStickyHeaderVariables(header))
            stickyHeaderObserver.unobserve(entry.target);
    }
});

window.followStickyHeader = function(header) {
    const container = header?.parentElement;
    if (!container)
        return;

    container.stickyHeader = header;
    stickyHeaderObserver.observe(header);
    stickyHeaderObserver.observe(container);
}

window.setDocumentLanguage = function (ietfTag) {
    document.documentElement.lang = ietfTag
}

window.formatChatInputMarkdown = function (inputId, formatType) {
    let input = document.getElementById(inputId)
    if (input && input.tagName !== 'TEXTAREA' && input.tagName !== 'INPUT')
        input = input.querySelector('textarea, input')

    if (!input)
        return ''

    input.focus()

    const value = input.value ?? ''
    const start = input.selectionStart ?? value.length
    const end = input.selectionEnd ?? value.length
    const hasSelection = end > start
    const selectedText = value.substring(start, end)

    let insertedText = ''
    let selectionStart = start
    let selectionEnd = start

    switch (formatType) {
        case 'bold': {
            const text = hasSelection ? selectedText : ''
            insertedText = `**${text}**`
            selectionStart = start + 2
            selectionEnd = selectionStart + text.length
            break
        }

        case 'italic': {
            const text = hasSelection ? selectedText : ''
            insertedText = `*${text}*`
            selectionStart = start + 1
            selectionEnd = selectionStart + text.length
            break
        }

        case 'heading': {
            if (hasSelection) {
                insertedText = selectedText
                    .split('\n')
                    .map(line => line.startsWith('# ') ? line : `# ${line}`)
                    .join('\n')

                selectionStart = start
                selectionEnd = start + insertedText.length
            } else {
                const text = ''
                insertedText = `# ${text}`
                selectionStart = start + 2
                selectionEnd = selectionStart + text.length
            }

            break
        }

        case 'bullet_list': {
            if (hasSelection) {
                insertedText = selectedText
                    .split('\n')
                    .map(line => line.startsWith('- ') ? line : `- ${line}`)
                    .join('\n')

                selectionStart = start
                selectionEnd = start + insertedText.length
            } else {
                insertedText = '- '
                selectionStart = start + 2
                selectionEnd = start + insertedText.length
            }

            break
        }

        case 'code':
        default: {
            if (hasSelection) {
                if (selectedText.includes('\n')) {
                    insertedText = `\`\`\`\n${selectedText}\n\`\`\``
                    selectionStart = start + 4
                    selectionEnd = selectionStart + selectedText.length
                } else {
                    insertedText = `\`${selectedText}\``
                    selectionStart = start + 1
                    selectionEnd = selectionStart + selectedText.length
                }
            } else {
                const text = ''
                insertedText = `\`${text}\``
                selectionStart = start + 1
                selectionEnd = selectionStart + text.length
            }

            break
        }
    }

    const nextValue = value.slice(0, start) + insertedText + value.slice(end)
    input.value = nextValue
    input.setSelectionRange(selectionStart, selectionEnd)
    input.dispatchEvent(new Event('input', { bubbles: true }))

    return nextValue
}

const escapeHandlers = new Map()

window.registerEscapeHandler = function (id, dotNetReference) {
    window.unregisterEscapeHandler(id)

    const handler = function (event) {
        if (event.key !== 'Escape')
            return

        event.preventDefault()
        event.stopPropagation()
        dotNetReference.invokeMethodAsync('HandleEscapeKeyAsync').catch(() => {})
    }

    document.addEventListener('keydown', handler, true)
    escapeHandlers.set(id, handler)
}

window.unregisterEscapeHandler = function (id) {
    const handler = escapeHandlers.get(id)
    if (!handler)
        return

    document.removeEventListener('keydown', handler, true)
    escapeHandlers.delete(id)
}

const localShortcutHandlers = new Map()

function tauriKeyFromKeyboardCode(code) {
    if (/^Key[A-Z]$/.test(code))
        return code.substring(3)

    if (/^Digit[0-9]$/.test(code))
        return code.substring(5)

    if (/^F(?:[1-9]|1[0-9]|2[0-4])$/.test(code))
        return code

    const keys = {
        Space: 'Space', Enter: 'Enter', Tab: 'Tab', Escape: 'Escape', Backspace: 'Backspace',
        Delete: 'Delete', Insert: 'Insert', Home: 'Home', End: 'End', PageUp: 'PageUp', PageDown: 'PageDown',
        ArrowUp: 'Up', ArrowDown: 'Down', ArrowLeft: 'Left', ArrowRight: 'Right',
        Numpad0: 'Num0', Numpad1: 'Num1', Numpad2: 'Num2', Numpad3: 'Num3', Numpad4: 'Num4',
        Numpad5: 'Num5', Numpad6: 'Num6', Numpad7: 'Num7', Numpad8: 'Num8', Numpad9: 'Num9',
        NumpadAdd: 'NumAdd', NumpadSubtract: 'NumSubtract', NumpadMultiply: 'NumMultiply',
        NumpadDivide: 'NumDivide', NumpadDecimal: 'NumDecimal', NumpadEnter: 'NumEnter',
        Minus: 'Minus', Equal: 'Equal', BracketLeft: 'BracketLeft', BracketRight: 'BracketRight',
        Backslash: 'Backslash', Semicolon: 'Semicolon', Quote: 'Quote', Backquote: 'Backquote',
        Comma: 'Comma', Period: 'Period', Slash: 'Slash'
    }

    return keys[code] ?? code
}

function parseTauriShortcut(shortcut) {
    const expected = { ctrl: false, shift: false, alt: false, meta: false, key: '' }
    const isMac = /Mac|iPhone|iPad|iPod/.test(navigator.platform)

    for (const rawPart of shortcut.split('+')) {
        const part = rawPart.trim().toLowerCase()
        switch (part) {
            case 'cmdorcontrol':
            case 'commandorcontrol':
                expected[isMac ? 'meta' : 'ctrl'] = true
                break
            case 'ctrl':
            case 'control':
                expected.ctrl = true
                break
            case 'cmd':
            case 'command':
            case 'meta':
            case 'super':
                expected.meta = true
                break
            case 'shift':
                expected.shift = true
                break
            case 'alt':
            case 'option':
                expected.alt = true
                break
            default:
                expected.key = rawPart.trim()
                break
        }
    }

    return expected
}

window.localShortcut = {
    register: function (id, shortcut, dotNetReference) {
        this.unregister(id)
        const expected = parseTauriShortcut(shortcut)
        if (!expected.key)
            return

        const handler = function (event) {
            if (event.repeat
                || event.ctrlKey !== expected.ctrl
                || event.shiftKey !== expected.shift
                || event.altKey !== expected.alt
                || event.metaKey !== expected.meta
                || tauriKeyFromKeyboardCode(event.code).toLowerCase() !== expected.key.toLowerCase())
                return

            event.preventDefault()
            event.stopPropagation()
            dotNetReference.invokeMethodAsync('OnLocalShortcutPressed').catch(() => {})
        }

        document.addEventListener('keydown', handler, true)
        localShortcutHandlers.set(id, handler)
    },

    unregister: function (id) {
        const handler = localShortcutHandlers.get(id)
        if (!handler)
            return

        document.removeEventListener('keydown', handler, true)
        localShortcutHandlers.delete(id)
    }
}

// What floats above the page without ever being a drop target. Two of these take part in hit testing as
// MudBlazor 9.12 stands: an open .mud-popover -- a closed one already declines pointer events through
// .mud-popover:not(.mud-popover-open) -- and .mud-snackbar, which asks for them explicitly with
// pointer-events: auto even though its container declines them, and snackbars appear constantly in this
// app. Without this list, a drag would be answered by whatever happens to float on screen rather than by
// the page below it. The remaining three are named because they surround those two: .mud-tooltip is the
// content of a popover, while #mud-snackbar-container and .mud-badge-wrapper carry pointer-events: none
// today and therefore never reach a hit test at all. Should a MudBlazor version drop that, they are
// covered here already. Children of all of them have to be skipped as well, which is why the test below
// uses closest rather than matches.
const skippedDropOverlays = '.mud-popover, .mud-tooltip, .mud-snackbar, #mud-snackbar-container, .mud-badge-wrapper'

// The drop zones of the app, addressed by the cursor position of a native drag and drop event.
//
// The arbitration between overlapping zones is left to the browser, and it can be: MudBlazor 9.12 gives
// neither .mud-dialog-container nor .mud-overlay a pointer-events: none. Both fill the viewport while a
// dialog is open, so a point beside the dialog box hits the container, and nothing there is a drop zone. A
//  drop, therefore, cannot reach through an open dialog into the page behind it -- the very thing the app
// used to enforce by counting layers in C#. That single CSS property carries this whole design, so it
// belongs on the checklist for every MudBlazor major version.
window.dropZones = {

    // Names the drop zone at the given viewport position, or null when there is none.
    //
    // The stack of elements is walked from the top down rather than asking for the topmost one alone,
    // because the topmost one may be an overlay from the list above and skipping it has to reveal what
    // lies beneath. The first element which is not skipped ends the walk, whether it belongs to a drop
    // zone or not: anything unknown blocks on purpose, so a drop can never slip through something the
    // user sees as being in the way. Within that element, closest resolves from the inside out, so a
    // specific zone inside a page-wide one wins -- which is exactly the precedence we want.
    hitTest: function (x, y) {
        for (const element of document.elementsFromPoint(x, y)) {
            if (element.closest(skippedDropOverlays))
                continue

            return element.closest('[data-drop-zone-id]')?.getAttribute('data-drop-zone-id') ?? null
        }

        return null
    },

    // Every drop zone currently in the DOM, in document order. This is for diagnostics only: when a drop
    // lands nowhere, it answers the question of which zones would have been available at that moment.
    list: function () {
        return Array.from(document.querySelectorAll('[data-drop-zone-id]'), zone => zone.getAttribute('data-drop-zone-id'))
    }
}