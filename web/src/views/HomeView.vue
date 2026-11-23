<script setup>
    import { computed, ref } from 'vue';

    import { useShowStore } from '@/stores/show';

    import BumpModal from '@/components/BumpModal.vue';
    import DonateModal from '@/components/DonateModal.vue';
    import MarqueeText from '@/components/MarqueeText.vue';
    import NowPlayingCard from '@/components/NowPlayingCard.vue';
    import SongRequestModal from '@/components/SongRequestModal.vue';

    const showStore = useShowStore();
    const showModal = ref(false);
    const showDonate = ref(false);
    const isDark = computed(() => showStore.config.theme !== 'light');
    const isOpen = computed(() => showStore.config.isOpen);
    const queueDepth = computed(() => showStore.queue.filter(q => q.status === 'Pending').length);

    function formatDuration(seconds) {
        if (!seconds) return '';
        const m = Math.floor(seconds / 60);
        const s = seconds % 60;
        return m > 0 ? `${m}m ${s > 0 ? s + 's' : ''}`.trim() : `${s}s`;
    }

    function estimatedWaitSeconds(itemIndex) {
        const items = showStore.queue;
        let secs = 0;
        if (items[0]?.status === 'Playing') secs += showStore.nowPlaying?.secondsRemaining || 0;
        for (let i = 0; i < itemIndex; i++) {
            if (items[i].status === 'Pending') secs += items[i].durationSeconds || 0;
        }
        return secs;
    }

    function formatWait(secs) {
        if (!secs) return null;
        if (secs < 60) return `~${secs}s`;
        return `~${Math.round(secs / 60)} min`;
    }

    const bumpItem = ref(null);
    function boost(item) {
        bumpItem.value = item;
    }

    const radioStyle = computed(() =>
        isDark.value
            ? 'bg-gradient-to-br from-slate-900 via-indigo-950 to-slate-900 border border-violet-400/35 shadow-[0_0_32px_rgba(139,92,246,0.15)]'
            : 'bg-gradient-to-br from-violet-50 via-violet-100 to-violet-50 border border-violet-400/30 shadow-[0_2px_16px_rgba(139,92,246,0.1)]'
    );

    const radioGlowStyle = computed(() => (isDark.value ? 'background: radial-gradient(ellipse at 50% 0%, rgba(139,92,246,0.2) 0%, transparent 70%)' : 'background: radial-gradient(ellipse at 50% 0%, rgba(139,92,246,0.1) 0%, transparent 70%)'));

    const radioIconStyle = computed(() => (isDark.value ? 'background: rgba(139,92,246,0.2); border: 1px solid rgba(139,92,246,0.3)' : 'background: rgba(139,92,246,0.15); border: 1px solid rgba(139,92,246,0.25)'));

    const radioLabelColor = computed(() => (isDark.value ? 'rgba(167,139,250,0.8)' : 'rgba(109,40,217,0.8)'));
    const radioSubColor = computed(() => (isDark.value ? 'rgba(167,139,250,0.6)' : 'rgba(109,40,217,0.6)'));
    const radioStationColor = computed(() => (isDark.value ? '#ffffff' : '#3b0764'));
</script>

<template>
    <div class="mx-auto max-w-lg px-4 pt-4 pb-6 md:max-w-2xl md:px-8 space-y-4">
        <!-- FM Radio banner -->
        <div v-if="showStore.config.fmRadioStation" class="relative overflow-hidden rounded-2xl" :class="radioStyle">
            <div class="pointer-events-none absolute inset-0" :style="radioGlowStyle" />
            <div class="relative flex items-center gap-4 px-5 py-4">
                <div class="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl text-2xl" :style="radioIconStyle">📻</div>
                <div class="min-w-0 flex-1">
                    <p class="mb-0.5 text-xs font-semibold tracking-widest uppercase" :style="`color: ${radioLabelColor}`">Tune your radio to</p>
                    <p class="text-2xl leading-none font-black tracking-tight md:text-4xl" :style="`color: ${radioStationColor}`">
                        {{ showStore.config.fmRadioStation }}
                    </p>
                    <p class="mt-1 text-xs" :style="`color: ${radioSubColor}`">to hear the show live</p>
                </div>
                <div class="flex shrink-0 items-center gap-0.5 pr-1">
                    <span
                        v-for="i in 4"
                        :key="i"
                        class="rounded-full"
                        :style="`width: 3px; height: ${6 + i * 4}px; opacity: ${0.3 + i * 0.18}; background: ${isDark ? '#a78bfa' : '#7c3aed'}; animation: radioPulse 1.2s ease-in-out infinite alternate; animation-delay: ${i * 0.15}s`"
                    />
                </div>
            </div>
        </div>

        <!-- Now Playing -->
        <NowPlayingCard />

        <!-- Closed notice -->
        <div
            v-if="!isOpen"
            class=" rounded-2xl border p-4 text-center"
            :class="
                isDark
                    ? 'border border-orange-400/35 bg-gradient-to-br from-orange-950 via-orange-900 to-orange-800 shadow-[0_4px_24px_rgba(197,81,34,0.2)]'
                    : 'border border-orange-400/40 bg-gradient-to-br from-orange-50 via-orange-100 to-orange-50 shadow-[0_2px_12px_rgba(197,81,34,0.15)]'
            "
        >
            <div class="mb-1 text-3xl">🌙</div>
            <p class="font-bold " :class="{'text-white/90':isDark, 'text-orange-400':!isDark}">Show requests are closed</p>
        </div>

        <!-- CTAs -->
        <div class="flex gap-3">
            <button v-if="isOpen" @click="showModal = true" class="cta cta-1 flex-1">
                <div class="cta-glow" />
                <div class="relative flex flex-col items-center gap-1 px-3 py-4">
                    <span class="cta-icon text-2xl leading-none"><font-awesome-icon :icon="['fas', 'music']" /></span>
                    <span class="cta-title text-sm font-black tracking-wide">Request a Song</span>
                    <span class="cta-chip rounded-full px-2.5 py-0.5 text-xs font-bold">${{ showStore.config.songRequestCost }}</span>
                </div>
            </button>

            <button @click="showDonate = true" class="cta cta-2 flex-1">
                <div class="cta-glow" />
                <div class="relative flex flex-col items-center gap-1 px-3 py-4">
                    <span class="cta-icon text-2xl leading-none"><font-awesome-icon :icon="['fas', 'gift']" /></span>
                    <span class="cta-title text-sm font-black tracking-wide">Donate</span>
                    <span class="cta-chip rounded-full px-2.5 py-0.5 text-xs font-bold">${{ showStore.config.donateCost }}+</span>
                </div>
            </button>
        </div>

        <!-- Up next preview -->
        <div v-if="showStore.queue.length > 0">
            <p class="text-hint mb-3 px-1 text-xs tracking-widest uppercase">Up Next</p>
            <div class="space-y-2">
                <div v-for="(item, i) in showStore.queue.slice(0, 3)" :key="item.id" class="overflow-hidden rounded-xl border" :class="item.status === 'Playing' ? 'card-live' : i === 0 ? 'border-ac3-soft' : 'border-base bg-card'">
                    <div class="flex items-center gap-3 px-3 py-2.5" :class="item.status === 'Playing' ? 'row-live' : i === 0 ? 'row-next' : ''">
                        <span class="shrink-0 text-lg">{{ item.status === 'Playing' ? '▶️' : i === 0 ? '1️⃣' : i === 1 ? '2️⃣' : '3️⃣' }}</span>
                        <div class="min-w-0 flex-1">
                            <MarqueeText :text="item.title" class="text-main text-sm font-medium" />
                            <div class="mt-0.5 flex items-center gap-2">
                                <p v-if="item.durationSeconds" class="text-hint text-xs">{{ formatDuration(item.durationSeconds) }}</p>
                                <p v-if="item.status === 'Pending' && estimatedWaitSeconds(i) > 0" class="text-xs" :class="i === 0 ? 'text-ac3 font-medium' : 'text-hint'">⏱ {{ formatWait(estimatedWaitSeconds(i)) }}</p>
                            </div>
                        </div>
                        <span v-if="item.status === 'Playing'" class="text-ac1 shrink-0 animate-pulse text-xs font-semibold">LIVE</span>
                        <button
                            v-if="item.status === 'Pending' && i !== showStore.queue.findIndex(q => q.status === 'Pending')"
                            @click="boost(item)"
                            class="chip-boost shrink-0 rounded-lg px-2.5 py-1.5 text-xs font-semibold transition active:scale-95"
                        >
                            {{ `🚀 $${showStore.config.bumpCost}` }}
                        </button>
                    </div>
                    <div v-if="item.status === 'Pending' && i === 0" class="border-ac3-soft bg-ac3-soft border-t px-3 py-1">
                        <span class="text-ac3 animate-pulse text-xs font-bold">⚡ YOU'RE UP NEXT!</span>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <SongRequestModal v-if="showModal" @close="showModal = false" />
    <DonateModal v-if="showDonate" @close="showDonate = false" />
    <BumpModal v-if="bumpItem" :item="bumpItem" @close="bumpItem = null" />
</template>

<style scoped>
    @keyframes radioPulse {
        from {
            opacity: 0.3;
            transform: scaleY(0.7);
        }
        to {
            opacity: 1;
            transform: scaleY(1.1);
        }
    }
</style>
