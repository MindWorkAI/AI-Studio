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

// Shared audio context for sound effects (Web Audio API does not register with Media Session):
let soundEffectContext = null;
const soundEffectCache = new Map();

window.playSound = async function(soundPath) {
    try {
        // Create or reuse the audio context:
        if (!soundEffectContext || soundEffectContext.state === 'closed') {
            soundEffectContext = new (window.AudioContext || window.webkitAudioContext)();
        }

        // Resume if suspended (browser autoplay policy):
        if (soundEffectContext.state === 'suspended') {
            await soundEffectContext.resume();
        }

        // Check the cache for already decoded audio:
        let audioBuffer = soundEffectCache.get(soundPath);

        if (!audioBuffer) {
            // Fetch and decode the audio file:
            const response = await fetch(soundPath);
            const arrayBuffer = await response.arrayBuffer();
            audioBuffer = await soundEffectContext.decodeAudioData(arrayBuffer);
            soundEffectCache.set(soundPath, audioBuffer);
        }

        // Create a new source node and play:
        const source = soundEffectContext.createBufferSource();
        source.buffer = audioBuffer;
        source.connect(soundEffectContext.destination);
        source.start(0);

    } catch (error) {
        console.warn('Failed to play sound effect:', error);
    }
};

let mediaRecorder;
let actualRecordingMimeType;
let changedMimeType = false;
let pendingChunkUploads = 0;

window.audioRecorder = {
    start: async function (dotnetRef, desiredMimeTypes = []) {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });

        // Play start recording sound effect:
        window.playSound('/sounds/start_recording.ogg');

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
                window.playSound('/sounds/stop_recording.ogg');

                // Stop all tracks to release the microphone:
                mediaRecorder.stream.getTracks().forEach(track => track.stop());
                
                // No need to process data here anymore, just signal completion:
                resolve({
                    mimeType: actualRecordingMimeType,
                    changedMimeType: changedMimeType,
                });
            };
            
            // Finally, stop the recording (which will actually trigger the onstop event):
            mediaRecorder.stop();
        });
    }
};