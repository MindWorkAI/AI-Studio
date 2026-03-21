const MATH_JAX_SCRIPT_ID = 'mudblazor-markdown-mathjax'
const MATH_JAX_SCRIPT_SRC = '_content/MudBlazor.Markdown/MudBlazor.Markdown.MathJax.min.js'
const INTERSECTION_ROOT_MARGIN = '240px 0px 240px 0px'
const MAX_TYPES_PER_BATCH = 4
const containerStates = new Map()
const pendingMathElements = new Set()

let mathJaxReadyPromise = null
let batchScheduled = false
let typesetInProgress = false

function isMathJaxReady() {
    return typeof window.MathJax?.typesetPromise === 'function' || typeof window.MathJax?.typeset === 'function'
}

function waitForMathJaxReady(attempt = 0) {
    if (isMathJaxReady())
        return Promise.resolve()

    if (attempt >= 80)
        return Promise.reject(new Error('MathJax did not finish loading in time.'))

    return new Promise((resolve, reject) => {
        window.setTimeout(() => {
            waitForMathJaxReady(attempt + 1).then(resolve).catch(reject)
        }, 50)
    })
}

function ensureMathJaxLoaded() {
    if (isMathJaxReady())
        return Promise.resolve()

    if (mathJaxReadyPromise)
        return mathJaxReadyPromise

    mathJaxReadyPromise = new Promise((resolve, reject) => {
        let script = document.getElementById(MATH_JAX_SCRIPT_ID)

        const onLoad = () => {
            waitForMathJaxReady().then(resolve).catch(reject)
        }

        const onError = () => reject(new Error('Failed to load the MathJax script.'))

        if (!script) {
            script = document.createElement('script')
            script.id = MATH_JAX_SCRIPT_ID
            script.type = 'text/javascript'
            script.src = MATH_JAX_SCRIPT_SRC
            script.addEventListener('load', onLoad, { once: true })
            script.addEventListener('error', onError, { once: true })
            document.head.appendChild(script)
            return
        }

        script.addEventListener('load', onLoad, { once: true })
        script.addEventListener('error', onError, { once: true })
        void waitForMathJaxReady().then(resolve).catch(() => {})
    }).catch(error => {
        mathJaxReadyPromise = null
        throw error
    })

    return mathJaxReadyPromise
}

function createContainerState() {
    return {
        signature: '',
        observer: null,
        observedElements: new Set()
    }
}

function disconnectContainerState(state) {
    if (state.observer) {
        state.observer.disconnect()
        state.observer = null
    }

    for (const element of state.observedElements)
        pendingMathElements.delete(element)

    state.observedElements.clear()
}

function isNearViewport(element) {
    const rect = element.getBoundingClientRect()
    return rect.bottom >= -240 && rect.top <= window.innerHeight + 240
}

function queueElementForTypeset(element, signature) {
    if (!element || !element.isConnected)
        return

    if (element.dataset.chatMathProcessedSignature === signature)
        return

    element.dataset.chatMathTargetSignature = signature
    element.dataset.chatMathPending = 'true'
    pendingMathElements.add(element)
    schedulePendingTypeset(false)
}

function schedulePendingTypeset(useIdleCallback) {
    if (batchScheduled)
        return

    batchScheduled = true
    const flush = () => {
        batchScheduled = false
        void flushPendingTypeset()
    }

    if (useIdleCallback && typeof window.requestIdleCallback === 'function') {
        window.requestIdleCallback(flush, { timeout: 120 })
        return
    }

    window.requestAnimationFrame(flush)
}

async function flushPendingTypeset() {
    if (typesetInProgress || pendingMathElements.size === 0)
        return

    typesetInProgress = true
    const elementsToTypeset = []

    try {
        await ensureMathJaxLoaded()

        for (const element of pendingMathElements) {
            if (elementsToTypeset.length >= MAX_TYPES_PER_BATCH)
                break

            if (!element.isConnected) {
                pendingMathElements.delete(element)
                continue
            }

            const targetSignature = element.dataset.chatMathTargetSignature ?? ''
            if (element.dataset.chatMathProcessedSignature === targetSignature) {
                pendingMathElements.delete(element)
                element.dataset.chatMathPending = 'false'
                continue
            }

            elementsToTypeset.push(element)
        }

        if (elementsToTypeset.length === 0)
            return

        for (const element of elementsToTypeset)
            pendingMathElements.delete(element)

        if (typeof window.MathJax?.typesetClear === 'function') {
            try {
                window.MathJax.typesetClear(elementsToTypeset)
            } catch (error) {
                console.warn('chatMath: failed to clear previous MathJax state.', error)
            }
        }

        if (typeof window.MathJax?.typesetPromise === 'function')
            await window.MathJax.typesetPromise(elementsToTypeset)
        else if (typeof window.MathJax?.typeset === 'function')
            window.MathJax.typeset(elementsToTypeset)

        for (const element of elementsToTypeset) {
            element.dataset.chatMathProcessedSignature = element.dataset.chatMathTargetSignature ?? ''
            element.dataset.chatMathPending = 'false'
        }
    } catch (error) {
        console.warn('chatMath: failed to typeset math content.', error)

        for (const element of elementsToTypeset)
            if (element.isConnected)
                pendingMathElements.add(element)
    } finally {
        typesetInProgress = false

        if (pendingMathElements.size > 0)
            schedulePendingTypeset(true)
    }
}

function createIntersectionObserver(state, signature) {
    return new IntersectionObserver(entries => {
        let queuedVisibleElement = false

        for (const entry of entries) {
            if (!entry.isIntersecting)
                continue

            const element = entry.target
            state.observer?.unobserve(element)
            state.observedElements.delete(element)
            queueElementForTypeset(element, signature)
            queuedVisibleElement = true
        }

        if (queuedVisibleElement)
            schedulePendingTypeset(true)
    }, {
        root: null,
        rootMargin: INTERSECTION_ROOT_MARGIN,
        threshold: 0.01
    })
}

function getMathElements(container) {
    return Array.from(container.querySelectorAll('.chat-mathjax-block'))
}

window.chatMath = {
    syncContainer: async function(container, signature) {
        if (!container)
            return

        let state = containerStates.get(container)
        if (!state) {
            state = createContainerState()
            containerStates.set(container, state)
        }

        if (state.signature === signature)
            return

        disconnectContainerState(state)
        state.signature = signature

        const mathElements = getMathElements(container)
        if (mathElements.length === 0)
            return

        await ensureMathJaxLoaded()

        state.observer = createIntersectionObserver(state, signature)

        for (const element of mathElements) {
            if (isNearViewport(element)) {
                queueElementForTypeset(element, signature)
                continue
            }

            element.dataset.chatMathTargetSignature = signature
            state.observer.observe(element)
            state.observedElements.add(element)
        }

        schedulePendingTypeset(false)
    },

    disposeContainer: function(container) {
        if (!container)
            return

        const state = containerStates.get(container)
        if (!state)
            return

        disconnectContainerState(state)
        containerStates.delete(container)

        const mathElements = getMathElements(container)
        for (const element of mathElements)
            pendingMathElements.delete(element)

        if (typeof window.MathJax?.typesetClear === 'function' && mathElements.length > 0) {
            try {
                window.MathJax.typesetClear(mathElements)
            } catch (error) {
                console.warn('chatMath: failed to clear container MathJax state during dispose.', error)
            }
        }
    }
}