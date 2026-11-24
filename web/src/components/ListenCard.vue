<script setup>
    import { computed, onMounted, onUnmounted } from 'vue';

    import { useListenStore } from '@/stores/listen';
    import { useShowStore } from '@/stores/show';

    /** Audio card: "Listen live" on the phone (when the stream is up) plus the FM station. */
    const showStore = useShowStore();
    const listen = useListenStore();

    const enabled = computed(() => showStore.config.listenEnabled);
    const playing = computed(() => listen.state === 'playing');
    const connecting = computed(() => listen.state === 'connecting');

    let timer;
    onMounted(() => {
        if (!enabled.value) return;
        listen.refreshStatus();
        timer = setInterval(listen.refreshStatus, 20000);
    });
    onUnmounted(() => clearInterval(timer));

    function toggle() {
        playing.value || connecting.value ? listen.stop() : listen.start();
    }
</script>

<template>
    <div v-if="(enabled && (listen.live || playing || connecting)) || showStore.config.fmRadioStation" class="bg-card border-base rounded-2xl border p-3">
        <div class="flex items-center gap-3">
            <button
                v-if="enabled && (listen.live || playing || connecting)"
                @click="toggle"
                class="listen-btn flex h-12 w-12 shrink-0 items-center justify-center rounded-full text-lg"
                :class="{ on: playing }"
                :aria-label="playing ? 'Stop listening' : 'Listen live'"
            >
                <span v-if="connecting" class="h-5 w-5 animate-spin rounded-full border-2 border-black/30 border-t-black" />
                <font-awesome-icon v-else :icon="['fas', playing ? 'stop' : 'headphones']" />
            </button>
            <font-awesome-icon v-else :icon="['fas', 'radio']" class="text-ac1 ml-1 text-lg" />

            <div class="min-w-0 flex-1">
                <template v-if="enabled && (listen.live || playing || connecting)">
                    <p class="text-main flex items-center gap-2 text-sm font-semibold">
                        {{ playing ? 'Listening live' : connecting ? 'Connecting…' : 'Listen on your phone' }}
                        <span v-if="playing" class="eq !h-3" aria-hidden="true">
                            <span v-for="i in 3" :key="i" :style="`--c: rgb(var(--ac1)); animation-delay: ${i * 0.15}s`" />
                        </span>
                    </p>
                    <p class="text-hint text-xs">
                        <template v-if="showStore.config.fmRadioStation">or tune your radio to <span class="text-main tabular font-semibold">{{ showStore.config.fmRadioStation }}</span></template>
                        <template v-else>In sync with the lights</template>
                    </p>
                </template>
                <p v-else class="text-sub text-sm">Tune your radio to <span class="tabular text-main text-base font-bold">{{ showStore.config.fmRadioStation }}</span></p>
            </div>
        </div>
        <p v-if="listen.error" class="mt-2 text-xs text-red-400">{{ listen.error }}</p>
    </div>
</template>

<style scoped>
    .listen-btn {
        color: #0a0a0a;
        background: rgb(var(--ac1));
        box-shadow: 0 0 0 4px rgb(var(--ac1) / 0.15);
        transition:
            transform 0.15s,
            box-shadow 0.2s;
    }
    .listen-btn:active {
        transform: scale(0.92);
    }
    .listen-btn.on {
        animation: ring 1.6s ease-out infinite;
    }
    @keyframes ring {
        0% {
            box-shadow: 0 0 0 0 rgb(var(--ac1) / 0.5);
        }
        100% {
            box-shadow: 0 0 0 14px rgb(var(--ac1) / 0);
        }
    }
</style>
