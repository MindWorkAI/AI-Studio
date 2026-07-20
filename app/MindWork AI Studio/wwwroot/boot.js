(() => {
    const maximumRetryCount = 12;
    const reconnectModal = document.getElementById('reconnect-modal');
    const retryDelaysMilliseconds = [
        0,
        1_000,
        2_000,
        5_000,
        10_000,
        15_000,
        30_000,
    ];

    let currentReconnectionProcess = null;
    let isConnectionDown = false;

    const delay = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));

    const getRetryDelayMilliseconds = attempt => retryDelaysMilliseconds[Math.min(attempt, retryDelaysMilliseconds.length - 1)];

    const showReconnectModal = () => {
        if (reconnectModal)
            reconnectModal.style.display = 'flex';
    };

    const hideReconnectModal = () => {
        if (reconnectModal)
            reconnectModal.style.display = 'none';
    };

    const setReconnectModalText = text => {
        if (reconnectModal)
            reconnectModal.textContent = text;
    };

    const startReconnectionProcess = () => {
        showReconnectModal();

        let isCanceled = false;
        let forceAttempt = false;

        const waitForNextAttempt = async milliseconds => {
            const startedAt = Date.now();
            while (!isCanceled && !forceAttempt && Date.now() - startedAt < milliseconds)
                await delay(250);

            forceAttempt = false;
        };

        void (async () => {
            for (let attempt = 0; attempt < maximumRetryCount && !isCanceled; attempt++) {
                setReconnectModalText(`Reconnecting to AI Studio (${attempt + 1}/${maximumRetryCount})...`);

                const retryDelayMilliseconds = getRetryDelayMilliseconds(attempt);
                if (retryDelayMilliseconds > 0)
                    await waitForNextAttempt(retryDelayMilliseconds);

                if (isCanceled)
                    return;

                try {
                    const result = await Blazor.reconnect();
                    if (result === false) {
                        // The server was reached, but the connection was rejected; reload the page.
                        location.reload();
                        return;
                    }

                    if (result === true)
                        return;
                } catch {
                    // Didn't reach the server; try again.
                }
            }

            // Retried too many times; reload the page.
            location.reload();
        })();

        return {
            cancel: () => {
                isCanceled = true;
                hideReconnectModal();
            },
            triggerImmediateAttempt: () => {
                forceAttempt = true;
            },
        };
    };

    const triggerReconnectAfterWake = () => {
        if (isConnectionDown)
            currentReconnectionProcess?.triggerImmediateAttempt();
    };

    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible')
            triggerReconnectAfterWake();
    });

    globalThis.addEventListener('pageshow', triggerReconnectAfterWake);

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
                }
            },

            configureSignalR: function (builder) {
                builder.withServerTimeout(120_000);
                builder.withKeepAliveInterval(30_000);
            },
        }
    });
})();