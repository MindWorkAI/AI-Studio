class PCMRecorderProcessor extends AudioWorkletProcessor {
    constructor(options) {
        super();

        const chunkDurationSeconds = options.processorOptions?.chunkDurationSeconds || 3;
        this.chunkSamples = Math.max(128, Math.round(sampleRate * chunkDurationSeconds));
        this.samples = new Int16Array(this.chunkSamples);
        this.numSamples = 0;

        this.port.onmessage = event => {
            if (event.data?.type === 'flush') {
                this.flush();
                this.port.postMessage({ type: 'flushed' });
            }
        };
    }

    process(inputs) {
        const channels = inputs[0];
        if (!channels || channels.length === 0)
            return true;

        const numFrames = channels[0].length;
        for (let frame = 0; frame < numFrames; frame++) {
            let monoSample = 0;
            for (const channel of channels) {
                monoSample += channel[frame] || 0;
            }

            monoSample = Math.max(-1, Math.min(1, monoSample / channels.length));
            this.samples[this.numSamples++] = monoSample < 0
                ? Math.round(monoSample * 0x8000)
                : Math.round(monoSample * 0x7fff);

            if (this.numSamples === this.chunkSamples)
                this.flush();
        }

        return true;
    }

    flush() {
        if (this.numSamples === 0)
            return;

        const buffer = new ArrayBuffer(this.numSamples * 2);
        const view = new DataView(buffer);
        for (let index = 0; index < this.numSamples; index++) {
            view.setInt16(index * 2, this.samples[index], true);
        }

        this.port.postMessage({
            type: 'chunk',
            buffer: buffer,
            sampleCount: this.numSamples,
        }, [buffer]);
        this.numSamples = 0;
    }
}

registerProcessor('pcm-recorder-processor', PCMRecorderProcessor);