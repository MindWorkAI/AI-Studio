window.generateDiff = function (text1, text2, divDiff, divLegend) {
    let wikEdDiff = new WikEdDiff();
    let targetDiv = document.getElementById(divDiff)
    targetDiv.innerHTML = wikEdDiff.diff(text1, text2);
    targetDiv.classList.add('mud-typography-body1', 'improvedDiff');
    
    let legend = document.getElementById(divLegend);
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
    targetDiv.innerHTML = '';
}

window.scrollToBottom = function(element) {
    element.scrollIntoView({ behavior: 'smooth', block: 'end', inline: 'nearest' });
}

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
const SOUND_GAP_SECONDS = 0.55;

// List of all sound effects used in the app:
const SOUND_EFFECT_PATHS = [
    '/sounds/start_recording.ogg',
    '/sounds/stop_recording.ogg',
    '/sounds/transcription_done.ogg'
];

// Initialize the audio context with low-latency settings.
// Should be called from a user interaction (click, keypress)
// to satisfy browser autoplay policies:
window.initSoundEffects = async function() {
    
    if (soundEffectContext && soundEffectContext.state !== 'closed') {
        // Already initialized, just ensure it's running:
        if (soundEffectContext.state === 'suspended') {
            await soundEffectContext.resume();
        }
        
        return;
    }

    try {
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

        // Preload all sound effects in parallel:
        if (!soundEffectsPreloaded) {
            await window.preloadSoundEffects();
        }
    } catch (error) {
        console.warn('Failed to initialize sound effects:', error);
    }
};

// Preload all sound effect files into the cache:
window.preloadSoundEffects = async function() {
    if (soundEffectsPreloaded) {
        return;
    }

    // Ensure that the context exists:
    if (!soundEffectContext || soundEffectContext.state === 'closed') {
        soundEffectContext = new (window.AudioContext || window.webkitAudioContext)({
            latencyHint: 'interactive'
        });
    }

    console.log('Sound effects - preloading', SOUND_EFFECT_PATHS.length, 'sound files...');

    const preloadPromises = SOUND_EFFECT_PATHS.map(async (soundPath) => {
        try {
            const response = await fetch(soundPath);
            const arrayBuffer = await response.arrayBuffer();
            const audioBuffer = await soundEffectContext.decodeAudioData(arrayBuffer);
            soundEffectCache.set(soundPath, audioBuffer);
            
            console.log('Sound effects - preloaded:', soundPath, 'duration:', audioBuffer.duration.toFixed(2), 's');
        } catch (error) {
            console.warn('Sound effects - failed to preload:', soundPath, error);
        }
    });

    await Promise.all(preloadPromises);
    soundEffectsPreloaded = true;
    console.log('Sound effects - all files preloaded');
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

let mediaRecorder;
let actualRecordingMimeType;
let changedMimeType = false;
let pendingChunkUploads = 0;

// Store the media stream so we can close the microphone later:
let activeMediaStream = null;

// Delay in milliseconds to wait after getUserMedia() for Bluetooth profile switch (A2DP → HFP):
const BLUETOOTH_PROFILE_SWITCH_DELAY_MS = 1_600;

window.audioRecorder = {
    start: async function (dotnetRef, desiredMimeTypes = []) {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        activeMediaStream = stream;

        // Wait for Bluetooth headsets to complete the profile switch from A2DP to HFP.
        // This prevents the first sound from being cut off during the switch:
        console.log('Audio recording - waiting for Bluetooth profile switch...');
        await new Promise(r => setTimeout(r, BLUETOOTH_PROFILE_SWITCH_DELAY_MS));

        // Play start recording sound effect:
        await window.playSound('/sounds/start_recording.ogg');

        // When only one mime type is provided as a string, convert it to an array:
        if (typeof desiredMimeTypes === 'string') {
            desiredMimeTypes = [desiredMimeTypes];
        }
        
        // Log sent mime types for debugging:
        console.log('Audio recording - requested mime types: ', desiredMimeTypes);

        let mimeTypes = desiredMimeTypes.filter(type => typeof type === 'string' && type.trim() !== '');
        
        // Next, we have to ensure that we have some default mime types to check as well.
        // In case the provided list does not contain these, we append them:
        // Use provided mime types or fallback to a default list:
        const defaultMimeTypes = [
            'audio/webm',
            'audio/ogg',
            'audio/mp4',
            'audio/mpeg',
            ''// Fallback to browser default
        ];
        
        defaultMimeTypes.forEach(type => {
            if (!mimeTypes.includes(type)) {
                mimeTypes.push(type);
            }
        });
        
        console.log('Audio recording - final mime types to check (included defaults): ', mimeTypes);

        // Find the first supported mime type:
        actualRecordingMimeType = mimeTypes.find(type =>
            type === '' || MediaRecorder.isTypeSupported(type)
        ) || '';

        console.log('Audio recording - the browser selected the following mime type for recording: ', actualRecordingMimeType);
        const options = actualRecordingMimeType ? { mimeType: actualRecordingMimeType } : {};
        mediaRecorder = new MediaRecorder(stream, options);

        // In case the browser changed the mime type:
        actualRecordingMimeType = mediaRecorder.mimeType;
        console.log('Audio recording - actual mime type used by the browser: ', actualRecordingMimeType);
        
        // Check the list of desired mime types against the actual one:
        if (!desiredMimeTypes.includes(actualRecordingMimeType)) {
            changedMimeType = true;
            console.warn(`Audio recording - requested mime types ('${desiredMimeTypes.join(', ')}') do not include the actual mime type used by the browser ('${actualRecordingMimeType}').`);
        } else {
            changedMimeType = false;
        }

        // Reset the pending uploads counter:
        pendingChunkUploads = 0;

        // Stream each chunk directly to .NET as it becomes available:
        mediaRecorder.ondataavailable = async (event) => {
            if (event.data.size > 0) {
                pendingChunkUploads++;
                try {
                    const arrayBuffer = await event.data.arrayBuffer();
                    const uint8Array = new Uint8Array(arrayBuffer);
                    await dotnetRef.invokeMethodAsync('OnAudioChunkReceived', uint8Array);
                } catch (error) {
                    console.error('Error sending audio chunk to .NET:', error);
                } finally {
                    pendingChunkUploads--;
                }
            }
        };

        mediaRecorder.start(3000); // read the recorded data in 3-second chunks
        return actualRecordingMimeType;
    },

    stop: async function () {
        return new Promise((resolve) => {
            
            // Add an event listener to handle the stop event:
            mediaRecorder.onstop = async () => {

                // Wait for all pending chunk uploads to complete before finalizing:
                console.log(`Audio recording - waiting for ${pendingChunkUploads} pending uploads.`);
                while (pendingChunkUploads > 0) {
                    await new Promise(r => setTimeout(r, 10)); // wait 10 ms before checking again
                }
                
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

                // No need to process data here anymore, just signal completion:
                resolve({
                    mimeType: actualRecordingMimeType,
                    changedMimeType: changedMimeType,
                });
            };
            
            // Finally, stop the recording (which will actually trigger the onstop event):
            mediaRecorder.stop();
        });
    },

    // Release the microphone after all sounds have been played.
    // This should be called after the transcription_done sound to allow
    // Bluetooth headsets to switch back to A2DP profile without interrupting audio:
    releaseMicrophone: function () {
        if (activeMediaStream) {
            console.log('Audio recording - releasing microphone (Bluetooth will switch back to A2DP)');
            activeMediaStream.getTracks().forEach(track => track.stop());
            activeMediaStream = null;
        }
    }
};