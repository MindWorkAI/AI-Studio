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

let mediaRecorder;
let audioChunks = [];
let actualRecordingMimeType;
let changedMimeType = false;

window.audioRecorder = {
    start: async function (desiredMimeTypes = []) {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });

        // When only one mime type is provided as a string, convert it to an array:
        if (typeof desiredMimeTypes === 'string') {
            desiredMimeTypes = [desiredMimeTypes];
        }
        
        // Log sent mime types for debugging:
        console.log('Requested mime types:', desiredMimeTypes);

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
        
        console.log('Final mime types to check (included defaults):', mimeTypes);

        // Find the first supported mime type:
        actualRecordingMimeType = mimeTypes.find(type =>
            type === '' || MediaRecorder.isTypeSupported(type)
        ) || '';

        console.log('Selected mime type for recording:', actualRecordingMimeType);
        const options = actualRecordingMimeType ? { mimeType: actualRecordingMimeType } : {};
        mediaRecorder = new MediaRecorder(stream, options);

        // In case the browser changed the mime type:
        actualRecordingMimeType = mediaRecorder.mimeType;
        
        // Check the list of desired mime types against the actual one:
        if (!desiredMimeTypes.includes(actualRecordingMimeType)) {
            changedMimeType = true;
            console.warn(`Requested mime types ('${desiredMimeTypes.join(', ')}') do not include the actual mime type used by MediaRecorder ('${actualRecordingMimeType}').`);
        } else {
            changedMimeType = false;
        }
        
        console.log('Actual mime type used by MediaRecorder:', actualRecordingMimeType);
        
        audioChunks = [];

        mediaRecorder.ondataavailable = (event) => {
            if (event.data.size > 0) {
                audioChunks.push(event.data);
            }
        };

        mediaRecorder.start(3000); // read the recorded data in 3-second chunks
        return actualRecordingMimeType;
    },

    stop: async function () {
        return new Promise((resolve) => {
            mediaRecorder.onstop = async () => {

                // Stop all tracks to release the microphone:
                mediaRecorder.stream.getTracks().forEach(track => track.stop());
                
                // Next, process the recorded audio data:
                const blob = new Blob(audioChunks, { type: actualRecordingMimeType });
                const arrayBuffer = await blob.arrayBuffer();
                const base64 = btoa(
                    new Uint8Array(arrayBuffer).reduce((data, byte) => data + String.fromCharCode(byte), '')
                );
                
                resolve({
                    data: base64,
                    mimeType: actualRecordingMimeType,
                    changedMimeType: changedMimeType,
                });
            };
            mediaRecorder.stop();
        });
    }
};