(() => {
    const MAXIMUM_RETRY_COUNT = 12;
    const reconnectModal = document.getElementById('reconnect-modal');
    const reconnectRecoveryHandlers = new Map();
    const reconnectRecoveredEventName = 'aistudio:reconnect-recovered';

    let currentReconnectionProcess = null;
    let isConnectionDown = false;

    const retryDelaysMilliseconds = [
        0,
        1_000,
        2_000,
        5_000,
        10_000,
        15_000,
        30_000,
    ];

    const delay = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));

    const getRetryDelayMilliseconds = attempt => retryDelaysMilliseconds[Math.min(attempt, retryDelaysMilliseconds.length - 1)];

    const setReconnectModalText = text => {
        if (!reconnectModal)
            return;

        reconnectModal.textContent = text;
    };

    const showReconnectModal = () => {
        if (!reconnectModal)
            return;

        reconnectModal.style.display = 'flex';
    };

    const hideReconnectModal = () => {
        if (!reconnectModal)
            return;

        reconnectModal.style.display = 'none';
    };

    const notifyReconnectRecovered = () => {
        window.dispatchEvent(new CustomEvent(reconnectRecoveredEventName));
    };

    const startReconnectionProcess = () => {
        showReconnectModal();

        let isCanceled = false;
        let forceAttempt = false;
        let isRunning = false;

        const waitForNextAttemptAsync = async milliseconds => {
            const startedAt = Date.now();
            while (!isCanceled && !forceAttempt && Date.now() - startedAt < milliseconds) {
                await delay(250);
            }

            forceAttempt = false;
        };

        const runAsync = async () => {
            if (isRunning)
                return;

            isRunning = true;

            try {
                for (let attempt = 0; attempt < MAXIMUM_RETRY_COUNT && !isCanceled; attempt++) {
                    setReconnectModalText(`Reconnecting to AI Studio (${attempt + 1}/${MAXIMUM_RETRY_COUNT})...`);

                    const delayMilliseconds = attempt == 0 ? 0 : getRetryDelayMilliseconds(attempt - 1);
                    if (delayMilliseconds > 0)
                        await waitForNextAttemptAsync(delayMilliseconds);

                    if (isCanceled)
                        return;

                    try {
                        const result = await Blazor.reconnect();
                        if (result === false) {
                            location.reload();
                            return;
                        }

                        if (result === true)
                            return;
                    } catch {
                        // Ignore transient transport failures and keep retrying.
                    }
                }

                if (!isCanceled)
                    location.reload();
            } finally {
                isRunning = false;
            }
        };

        void runAsync();

        return {
            cancel: () => {
                isCanceled = true;
                hideReconnectModal();
            },
            triggerImmediateAttempt: () => {
                forceAttempt = true;
                void runAsync();
            },
        };
    };

    const triggerReconnectAfterWake = () => {
        if (!isConnectionDown)
            return;

        currentReconnectionProcess?.triggerImmediateAttempt();
    };

    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible')
            triggerReconnectAfterWake();
    });

    window.addEventListener('pageshow', () => {
        triggerReconnectAfterWake();
    });

    window.registerReconnectRecovery = function (id, dotNetReference) {
        window.unregisterReconnectRecovery(id);

        const handler = function () {
            dotNetReference.invokeMethodAsync('HandleReconnectRecoveryAsync').catch(() => {});
        };

        window.addEventListener(reconnectRecoveredEventName, handler);
        reconnectRecoveryHandlers.set(id, handler);
    };

    window.unregisterReconnectRecovery = function (id) {
        const handler = reconnectRecoveryHandlers.get(id);
        if (!handler)
            return;

        window.removeEventListener(reconnectRecoveredEventName, handler);
        reconnectRecoveryHandlers.delete(id);
    };

    Blazor.start({
        circuit: {
            reconnectionHandler: {
                onConnectionDown: () => {
                    isConnectionDown = true;
                    currentReconnectionProcess ??= startReconnectionProcess();
                },
                onConnectionUp: () => {
                    isConnectionDown = false;
                    currentReconnectionProcess?.cancel();
                    currentReconnectionProcess = null;
                    notifyReconnectRecovered();
                },
            },

            configureSignalR: function (builder) {
                builder.withServerTimeout(120_000);
                builder.withKeepAliveInterval(30_000);
            },
        }
    });
})();