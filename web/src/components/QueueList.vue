<script setup>
    import { computed, ref } from 'vue';

    import { useShowStore } from '@/stores/show';

    import BumpModal from '@/components/BumpModal.vue';
    import MarqueeText from '@/components/MarqueeText.vue';

    const showStore = useShowStore();

    const bumpItem = ref(null);
    function bump(item) {
        bumpItem.value = item;
    }

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

    const firstPendingIndex = computed(() => showStore.queue.findIndex(q => q.status === 'Pending'));

    const positionEmoji = ['🥇', '🥈', '🥉', '4️⃣', '5️⃣', '6️⃣', '7️⃣', '8️⃣', '9️⃣'];
</script>

<template>
    <div>
        <!-- Empty state -->
        <div v-if="showStore.queue.length === 0" class="px-4 py-12 text-center">
            <div class="mb-3 text-5xl">✨</div>
            <p class="text-main text-lg font-bold">Queue is empty!</p>
            <p class="text-hint mt-1 text-sm">Be the first to light up the night</p>
        </div>

        <!-- Queue items -->
        <div v-else class="space-y-3">
            <div v-for="(item, i) in showStore.queue" :key="item.id" class="overflow-hidden rounded-2xl border transition" :class="item.status === 'Playing' ? 'card-live card-live-bg' : i === 0 ? 'border-ac3-soft row-next' : 'border-base bg-card'">
                <div class="flex items-center gap-3 px-4 py-3.5">
                    <div class="w-8 shrink-0 text-center text-2xl">
                        {{ item.status === 'Playing' ? '▶️' : (positionEmoji[i] ?? `${i + 1}`) }}
                    </div>

                    <div class="min-w-0 flex-1">
                        <MarqueeText :text="item.title" class="text-main text-sm leading-snug font-semibold" />
                        <p class="text-hint mt-0.5 truncate text-xs">{{ item.artist || item.playlistName }}</p>
                        <div class="mt-1 flex items-center gap-2">
                            <span v-if="item.durationSeconds" class="text-hint text-xs">
                                {{ formatDuration(item.durationSeconds) }}
                            </span>
                            <span v-if="item.status === 'Pending' && estimatedWaitSeconds(i) > 0" class="text-xs font-medium" :class="i === 0 ? 'text-ac3' : 'text-hint'">{{ i === 0 ? '⚡ ' : '⏱ ' }}{{ formatWait(estimatedWaitSeconds(i)) }} away</span>
                        </div>
                    </div>

                    <div class="flex shrink-0 flex-col items-end gap-1.5">
                        <span v-if="item.status === 'Playing'" class="bg-ac1-soft text-ac1 animate-pulse rounded-full px-2 py-0.5 text-xs font-bold">LIVE</span>
                        <button
                            v-if="item.status === 'Pending' && i !== firstPendingIndex"
                            @click="bump(item)"
                            class="chip-boost rounded-xl px-3 py-1.5 text-xs font-semibold transition active:scale-95"
                        >
                            {{ `🚀 Boost $${showStore.config.bumpCost}` }}
                        </button>
                    </div>
                </div>

                <div v-if="item.status === 'Pending' && i === 0" class="border-ac3-soft bg-ac3-soft flex items-center gap-2 border-t px-4 py-1.5">
                    <span class="text-ac3 animate-pulse text-xs font-bold">⚡ YOU'RE UP NEXT — GET READY!</span>
                </div>
            </div>
        </div>

        <BumpModal v-if="bumpItem" :item="bumpItem" @close="bumpItem = null" />
    </div>
</template>
