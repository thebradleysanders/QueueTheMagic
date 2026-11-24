import axios from 'axios';
import { defineStore } from 'pinia';

import { ref } from 'vue';

/**
 * "Listen live". The server relays FPP's Opus audio packets over a WebSocket on the site's normal
 * port (no WebRTC, no port forwarding). We decode each 20 ms packet and schedule it on a Web
 * Audio clock with a small, self-adjusting buffer:
 *   - start at TARGET seconds of buffer,
 *   - if the network stalls and we run dry, restart at TARGET (+ a little more after repeated stalls),
 *   - if packets pile up (after a stall), drop audio to get back down, so delay never creeps up.
 * Lives in a store so the audio keeps playing while the visitor moves between pages.
 */
const TARGET = 0.12; // seconds of buffer we aim for
const MAX_TARGET = 0.4; // never let a bad connection push us past this
const DROP_ABOVE = 0.25; // seconds above target before we start skipping to catch up

export const useListenStore = defineStore('listen', () => {
    const state = ref('idle'); // idle | connecting | playing | error
    const error = ref(null);
    const live = ref(false); // is FPP audio arriving at the server?
    const bufferMs = ref(0); // current buffered audio, for the curious

    let ctx = null;
    let ws = null;
    let decoder = null;
    let playAt = 0;
    let target = TARGET;
    let stalls = 0;
    let gain = null;

    async function refreshStatus() {
        try {
            const { data } = await axios.get('/api/listen/status');
            live.value = data.enabled && data.live;
        } catch {
            live.value = false;
        }
    }

    function schedule(channelData, samples, sampleRate) {
        if (!samples || !ctx) return;
        const now = ctx.currentTime;
        const ahead = playAt - now;

        if (ahead < 0.005) {
            // Ran dry (network hiccup): restart with a slightly bigger cushion if it keeps happening.
            stalls++;
            if (stalls > 2) target = Math.min(MAX_TARGET, target + 0.03);
            playAt = now + target;
        } else if (ahead > target + DROP_ABOVE) {
            // Backlog after a stall: skip this packet so we stay close to the lights.
            bufferMs.value = Math.round(ahead * 1000);
            return;
        }

        const buf = ctx.createBuffer(channelData.length, samples, sampleRate);
        channelData.forEach((d, i) => buf.copyToChannel(d.subarray(0, samples), i));
        const src = ctx.createBufferSource();
        src.buffer = buf;
        src.connect(gain);
        src.start(playAt);
        playAt += samples / sampleRate;
        bufferMs.value = Math.round((playAt - now) * 1000);
        if (state.value !== 'playing') state.value = 'playing';
    }

    async function start() {
        if (state.value === 'connecting' || state.value === 'playing') return;
        state.value = 'connecting';
        error.value = null;

        // Create/resume the audio clock inside the tap so iOS allows sound.
        try {
            const AC = window.AudioContext || window.webkitAudioContext;
            ctx = new AC({ latencyHint: 'interactive', sampleRate: 48000 });
        } catch {
            ctx = new (window.AudioContext || window.webkitAudioContext)();
        }
        ctx.resume();
        gain = ctx.createGain();
        gain.connect(ctx.destination);
        playAt = 0;
        target = TARGET;
        stalls = 0;

        try {
            // Only fetched when someone actually taps Listen.
            const { OpusDecoder } = await import('opus-decoder');
            decoder = new OpusDecoder({ channels: 2, forceStereo: true, preSkip: 0 });
            await decoder.ready;
        } catch {
            return fail('Live audio is not supported on this browser.');
        }

        const proto = location.protocol === 'https:' ? 'wss' : 'ws';
        ws = new WebSocket(`${proto}://${location.host}/api/listen/ws`);
        ws.binaryType = 'arraybuffer';
        ws.onmessage = e => {
            if (typeof e.data === 'string' || !decoder) return; // first message is just the format
            try {
                const { channelData, samplesDecoded, sampleRate } = decoder.decodeFrame(new Uint8Array(e.data));
                schedule(channelData, samplesDecoded, sampleRate);
            } catch {
                /* skip a bad packet */
            }
        };
        ws.onclose = e => {
            if (state.value === 'idle') return;
            fail(e.reason === 'full' ? 'The live audio is full right now. Try the radio, or try again in a few minutes.' : 'The audio connection dropped. Tap Listen to reconnect.');
        };
        ws.onerror = () => {};
    }

    function cleanup() {
        if (ws) {
            ws.onclose = null;
            ws.close();
        }
        ws = null;
        try {
            decoder?.free();
        } catch {
            /* ignore */
        }
        decoder = null;
        ctx?.close().catch(() => {});
        ctx = null;
        bufferMs.value = 0;
    }

    function fail(message) {
        cleanup();
        state.value = 'error';
        error.value = message;
    }

    function stop() {
        state.value = 'idle';
        error.value = null;
        cleanup();
    }

    if (typeof window !== 'undefined') window.addEventListener('pagehide', () => state.value !== 'idle' && stop());

    return { state, error, live, bufferMs, refreshStatus, start, stop };
});
