// Shared the audio context for sound effects (Web Audio API does not register with Media Session):
let soundEffectContext = null;

// Cache for decoded sound effect audio buffers:
const soundEffectCache = new Map();

// Track the preload state:
let soundEffectsPreloaded = false;

// Queue system: tracks when the next sound can start playing.
// This prevents sounds from overlapping and getting "swallowed" by the audio system:
let nextAvailablePlayTime = 0;

// Minimum gap between sounds in seconds (small buffer to ensure clean transitions):
const SOUND_GAP_SECONDS = 0.25;

// List of all sound effects used in the app:
const SOUND_EFFECT_PATHS = [
    '/sounds/start_recording.ogg',
    '/sounds/stop_recording.ogg',
    '/sounds/transcription_done.ogg'
];

function createSoundEffectsInitResult(success, failedPaths = [], errorMessage = null) {
    return {
        success: success,
        failedPaths: failedPaths,
        errorMessage: errorMessage
    };
}

// Initialize the audio context with low-latency settings.
// Should be called from a user interaction (click, keypress)
// to satisfy browser autoplay policies:
window.initSoundEffects = async function() {

    try {
        if (soundEffectContext && soundEffectContext.state !== 'closed') {
            // Already initialized, just ensure it's running:
            if (soundEffectContext.state === 'suspended') {
                await soundEffectContext.resume();
            }
        } else {
            // Create the context with the interactive latency hint for the lowest latency:
            soundEffectContext = new (window.AudioContext || window.webkitAudioContext)({
                latencyHint: 'interactive'
            });

            // Resume immediately (needed for Safari/macOS):
            if (soundEffectContext.state === 'suspended') {
                await soundEffectContext.resume();
            }

            // Reset the queue timing:
            nextAvailablePlayTime = 0;

            //
            // Play a very short silent buffer to "warm up" the audio pipeline.
            // This helps prevent the first real sound from being cut off:
            //
            const silentBuffer = soundEffectContext.createBuffer(1, 1, soundEffectContext.sampleRate);
            const silentSource = soundEffectContext.createBufferSource();
            silentSource.buffer = silentBuffer;
            silentSource.connect(soundEffectContext.destination);
            silentSource.start(0);

            console.log('Sound effects - AudioContext initialized with latency:', soundEffectContext.baseLatency);
        }

        // Preload all sound effects in parallel:
        if (!soundEffectsPreloaded) {
            return await window.preloadSoundEffects();
        }

        return createSoundEffectsInitResult(true);
    } catch (error) {
        console.warn('Failed to initialize sound effects:', error);
        return createSoundEffectsInitResult(false, [], error?.message || String(error));
    }
};

// Preload all sound effect files into the cache:
window.preloadSoundEffects = async function() {
    if (soundEffectsPreloaded) {
        return createSoundEffectsInitResult(true);
    }

    // Ensure that the context exists:
    if (!soundEffectContext || soundEffectContext.state === 'closed') {
        soundEffectContext = new (window.AudioContext || window.webkitAudioContext)({
            latencyHint: 'interactive'
        });
    }

    console.log('Sound effects - preloading', SOUND_EFFECT_PATHS.length, 'sound files...');
    const failedPaths = [];

    const preloadPromises = SOUND_EFFECT_PATHS.map(async (soundPath) => {
        try {
            const response = await fetch(soundPath);
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const arrayBuffer = await response.arrayBuffer();
            const audioBuffer = await soundEffectContext.decodeAudioData(arrayBuffer);
            soundEffectCache.set(soundPath, audioBuffer);

            console.log('Sound effects - preloaded:', soundPath, 'duration:', audioBuffer.duration.toFixed(2), 's');
        } catch (error) {
            console.warn('Sound effects - failed to preload:', soundPath, error);
            failedPaths.push(soundPath);
        }
    });

    await Promise.all(preloadPromises);
    soundEffectsPreloaded = failedPaths.length === 0;

    if (soundEffectsPreloaded) {
        console.log('Sound effects - all files preloaded');
        return createSoundEffectsInitResult(true);
    }

    console.warn('Sound effects - preload finished with failures:', failedPaths);
    return createSoundEffectsInitResult(false, failedPaths, 'One or more sound effects could not be loaded.');
};

window.playSound = async function(soundPath) {
    try {
        // Initialize context if needed (fallback if initSoundEffects wasn't called):
        if (!soundEffectContext || soundEffectContext.state === 'closed') {
            soundEffectContext = new (window.AudioContext || window.webkitAudioContext)({
                latencyHint: 'interactive'
            });

            nextAvailablePlayTime = 0;
        }

        // Resume if suspended (browser autoplay policy):
        if (soundEffectContext.state === 'suspended') {
            await soundEffectContext.resume();
        }

        // Check the cache for already decoded audio:
        let audioBuffer = soundEffectCache.get(soundPath);

        if (!audioBuffer) {
            // Fetch and decode the audio file (fallback if not preloaded):
            console.log('Sound effects - loading on demand:', soundPath);
            const response = await fetch(soundPath);
            const arrayBuffer = await response.arrayBuffer();
            audioBuffer = await soundEffectContext.decodeAudioData(arrayBuffer);
            soundEffectCache.set(soundPath, audioBuffer);
        }

        // Calculate when this sound should start:
        const currentTime = soundEffectContext.currentTime;
        let startTime;

        if (currentTime >= nextAvailablePlayTime) {
            // No sound is playing, or the previous sound has finished; start immediately:
            startTime = 0; // 0 means "now" in Web Audio API
            nextAvailablePlayTime = currentTime + audioBuffer.duration + SOUND_GAP_SECONDS;
        } else {
            // A sound is still playing; schedule this sound to start after it:
            startTime = nextAvailablePlayTime;
            nextAvailablePlayTime = startTime + audioBuffer.duration + SOUND_GAP_SECONDS;
            console.log('Sound effects - queued:', soundPath, 'will play in', (startTime - currentTime).toFixed(2), 's');
        }

        // Create a new source node and schedule playback:
        const source = soundEffectContext.createBufferSource();
        source.buffer = audioBuffer;
        source.connect(soundEffectContext.destination);
        source.start(startTime);
        console.log('Sound effects - playing:', soundPath);

    } catch (error) {
        console.warn('Failed to play sound effect:', error);
    }
};

let pendingChunkUploads = 0;
let chunkUploadPromise = Promise.resolve();
let chunkUploadError = null;
let recordingError = null;
let captureAudioContext = null;
let captureSourceNode = null;
let captureWorkletNode = null;
let captureSilentGainNode = null;
let pcmFlushResolve = null;
let pcmSamplesReceived = 0;

// Store the media stream so we can close the microphone later:
let activeMediaStream = null;

// Delay in milliseconds to wait after getUserMedia() for Bluetooth profile switch (A2DP → HFP):
const BLUETOOTH_PROFILE_SWITCH_DELAY_MS = 1_600;
const PCM_SAMPLE_RATE = 48_000;
const PCM_CHUNK_DURATION_SECONDS = 3;
const PCM_FLUSH_TIMEOUT_MS = 5_000;

function queueAudioChunkUpload(upload) {
    pendingChunkUploads++;
    chunkUploadPromise = chunkUploadPromise
        .then(upload)
        .catch(error => {
            chunkUploadError ??= error;
            console.error('Error sending audio chunk to .NET:', error);
        })
        .finally(() => pendingChunkUploads--);
}

async function waitForAudioChunkUploads() {
    let observedUploadPromise;
    do {
        observedUploadPromise = chunkUploadPromise;
        await observedUploadPromise;
    } while (pendingChunkUploads > 0 || observedUploadPromise !== chunkUploadPromise);
}

function createPcmWavHeader(sampleRate) {
    const buffer = new ArrayBuffer(44);
    const view = new DataView(buffer);

    const writeAscii = (offset, value) => {
        for (let index = 0; index < value.length; index++) {
            view.setUint8(offset + index, value.charCodeAt(index));
        }
    };

    writeAscii(0, 'RIFF');
    view.setUint32(4, 0, true); // Finalized by .NET after all PCM data was written.
    writeAscii(8, 'WAVE');
    writeAscii(12, 'fmt ');
    view.setUint32(16, 16, true);
    view.setUint16(20, 1, true); // PCM
    view.setUint16(22, 1, true); // Mono
    view.setUint32(24, sampleRate, true);
    view.setUint32(28, sampleRate * 2, true);
    view.setUint16(32, 2, true);
    view.setUint16(34, 16, true);
    writeAscii(36, 'data');
    view.setUint32(40, 0, true); // Finalized by .NET after all PCM data was written.

    return new Uint8Array(buffer);
}

function observeAudioTrack(track) {
    console.log('Audio recording - microphone track state:', {
        label: track.label,
        enabled: track.enabled,
        muted: track.muted,
        readyState: track.readyState,
        settings: typeof track.getSettings === 'function' ? track.getSettings() : null,
    });

    track.addEventListener('mute', () => console.warn('Audio recording - microphone track was muted.'));
    track.addEventListener('unmute', () => console.log('Audio recording - microphone track was unmuted.'));
    track.addEventListener('ended', () => console.warn('Audio recording - microphone track ended.'));
}

async function startPcmRecording(stream, dotnetRef) {
    const AudioContextClass = window.AudioContext || window.webkitAudioContext;
    if (!AudioContextClass || typeof AudioWorkletNode === 'undefined') {
        throw new Error('PCM audio capture is unavailable because AudioWorklet is not supported.');
    }

    try {
        captureAudioContext = new AudioContextClass({
            latencyHint: 'interactive',
            sampleRate: PCM_SAMPLE_RATE,
        });

        if (!captureAudioContext.audioWorklet) {
            throw new Error('PCM audio capture is unavailable because AudioWorklet is not supported.');
        }

        await captureAudioContext.audioWorklet.addModule('/audio-recorder-worklet.js');

        const actualSampleRate = captureAudioContext.sampleRate;
        console.log(`Audio recording - starting PCM/WAV capture at ${actualSampleRate} Hz mono.`);

        if (captureAudioContext.state === 'suspended') {
            await captureAudioContext.resume();
        }

        captureSourceNode = captureAudioContext.createMediaStreamSource(stream);
        captureWorkletNode = new AudioWorkletNode(captureAudioContext, 'pcm-recorder-processor', {
            numberOfInputs: 1,
            numberOfOutputs: 1,
            outputChannelCount: [1],
            processorOptions: {
                chunkDurationSeconds: PCM_CHUNK_DURATION_SECONDS,
            },
        });

        captureSilentGainNode = captureAudioContext.createGain();
        captureSilentGainNode.gain.value = 0;

        captureWorkletNode.port.onmessage = event => {
            if (event.data?.type === 'chunk') {
                const chunkBytes = new Uint8Array(event.data.buffer);
                pcmSamplesReceived += event.data.sampleCount;
                console.debug(`Audio recording - received ${event.data.sampleCount} PCM samples from AudioWorklet.`);
                queueAudioChunkUpload(() => dotnetRef.invokeMethodAsync('OnAudioChunkReceived', chunkBytes));
            } else if (event.data?.type === 'flushed') {
                pcmFlushResolve?.();
                pcmFlushResolve = null;
            }
        };

        captureWorkletNode.onprocessorerror = event => {
            recordingError ??= event.error || new Error('The PCM audio processor failed.');
            console.error('Audio recording - AudioWorklet error:', recordingError);
        };

        captureSourceNode.connect(captureWorkletNode);
        captureWorkletNode.connect(captureSilentGainNode);
        captureSilentGainNode.connect(captureAudioContext.destination);
        queueAudioChunkUpload(() => dotnetRef.invokeMethodAsync('OnAudioChunkReceived', createPcmWavHeader(actualSampleRate)));
    } catch (error) {
        await cleanupPcmCapture();
        throw error;
    }
}

async function flushPcmRecording() {
    if (!captureWorkletNode) {
        throw new Error('The PCM audio processor is unavailable.');
    }

    await new Promise((resolve, reject) => {
        const timeoutId = setTimeout(() => {
            pcmFlushResolve = null;
            reject(new Error('Timed out while flushing PCM audio data.'));
        }, PCM_FLUSH_TIMEOUT_MS);

        pcmFlushResolve = () => {
            clearTimeout(timeoutId);
            resolve();
        };
        captureWorkletNode.port.postMessage({ type: 'flush' });
    });
}

async function cleanupPcmCapture() {
    captureSourceNode?.disconnect();
    captureWorkletNode?.disconnect();
    captureSilentGainNode?.disconnect();
    captureSourceNode = null;
    captureWorkletNode = null;
    captureSilentGainNode = null;
    pcmFlushResolve = null;

    if (captureAudioContext && captureAudioContext.state !== 'closed') {
        await captureAudioContext.close();
    }
    captureAudioContext = null;
}

window.audioRecorder = {
    start: async function (dotnetRef) {
        // Reset the upload and recorder state:
        pendingChunkUploads = 0;
        chunkUploadPromise = Promise.resolve();
        chunkUploadError = null;
        recordingError = null;
        pcmSamplesReceived = 0;

        const stream = await navigator.mediaDevices.getUserMedia({
            audio: {
                sampleRate: { ideal: PCM_SAMPLE_RATE },
                channelCount: { ideal: 1 },
            },
        });
        activeMediaStream = stream;

        const audioTracks = stream.getAudioTracks();
        if (audioTracks.length === 0) {
            throw new Error('The microphone stream does not contain an audio track.');
        }
        observeAudioTrack(audioTracks[0]);

        // Wait for Bluetooth headsets to complete the profile switch from A2DP to HFP.
        // This prevents the first sound from being cut off during the switch:
        console.log('Audio recording - waiting for Bluetooth profile switch...');
        await new Promise(r => setTimeout(r, BLUETOOTH_PROFILE_SWITCH_DELAY_MS));

        // Play start recording sound effect:
        await window.playSound('/sounds/start_recording.ogg');

        await startPcmRecording(stream, dotnetRef);
    },

    stop: async function () {
        let stopError = null;

        try {
            try {
                await flushPcmRecording();
            } finally {
                await cleanupPcmCapture();
            }

            console.log(`Audio recording - PCM/WAV capture produced ${pcmSamplesReceived} samples.`);
            if (pcmSamplesReceived === 0) {
                throw new Error('The microphone did not produce any PCM audio samples.');
            }
        } catch (error) {
            stopError = error;
        }

        console.log(`Audio recording - waiting for ${pendingChunkUploads} pending uploads.`);
        await waitForAudioChunkUploads();
        console.log('Audio recording - all chunks uploaded, finalizing.');

        // Play stop recording sound effect:
        await window.playSound('/sounds/stop_recording.ogg');

        //
        // IMPORTANT: Do NOT release the microphone here!
        // Bluetooth headsets switch profiles (HFP → A2DP) when the microphone is released,
        // which causes audio to be interrupted. We keep the microphone open so that the
        // stop_recording and transcription_done sounds can play without interruption.
        //
        // Call window.audioRecorder.releaseMicrophone() after the last sound has played.
        //

        const error = stopError || recordingError || chunkUploadError;
        if (error) {
            throw error;
        }
    },

    // Release the microphone after all sounds have been played.
    // This should be called after the transcription_done sound to allow
    // Bluetooth headsets to switch back to A2DP profile without interrupting audio:
    releaseMicrophone: async function () {
        await cleanupPcmCapture();

        if (activeMediaStream) {
            console.log('Audio recording - releasing microphone (Bluetooth will switch back to A2DP)');
            activeMediaStream.getTracks().forEach(track => track.stop());
            activeMediaStream = null;
        }
    }
};
