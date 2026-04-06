<script setup>
    import axios from 'axios';

    import { computed, ref } from 'vue';

    import { usePayment } from '@/composables/usePayment';

    import { useSessionStore } from '@/stores/session';
    import { useShowStore } from '@/stores/show';

    import MarqueeText from '@/components/MarqueeText.vue';

    const showStore = useShowStore();
    const sessionStore = useSessionStore();
    const { createIntent, confirmPayment } = usePayment();
    const bumpingId = ref(null);
    const isDev = import.meta.env.DEV;
    const isDark = computed(() => showStore.config.theme !== 'light');

    async function bump(item) {
        if (bumpingId.value) return;
        bumpingId.value = item.id;
        try {
            let paymentIntentId;
            if (isDev) {
                paymentIntentId = 'dev_test';
            } else {
                const clientSecret = await createIntent('bump', item.id);
                paymentIntentId = await confirmPayment(clientSecret, window.location.href);
            }
            await axios.put(`/api/queue/${item.id}/bump`, {
                paymentIntentId,
                sessionToken: sessionStore.sessionToken
            });
            await showStore.fetchQueue();
        } catch (e) {
            alert(e.message || 'Boost failed');
        } finally {
            bumpingId.value = null;
        }
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

    function rowStyle(item, i) {
        if (item.status === 'Playing') {
            return isDark.value ? 'background: linear-gradient(to right, #052e16, #111827)' : 'background: linear-gradient(to right, #dcfce7, #ffffff)';
        }
        if (i === 0) {
            return isDark.value ? 'background: linear-gradient(to right, rgba(66,32,6,0.6), #111827)' : 'background: linear-gradient(to right, #fef9c3, #ffffff)';
        }
        return '';
    }
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
            <div v-for="(item, i) in showStore.queue" :key="item.id" class="overflow-hidden rounded-2xl border transition" :class="item.status === 'Playing' ? 'border-green-500/40' : i === 0 ? 'border-yellow-500/30' : 'border-base bg-card'" :style="rowStyle(item, i)">
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
                            <span v-if="item.status === 'Pending' && estimatedWaitSeconds(i) > 0" class="text-xs font-medium" :class="i === 0 ? 'text-yellow-500' : 'text-hint'">{{ i === 0 ? '⚡ ' : '⏱ ' }}{{ formatWait(estimatedWaitSeconds(i)) }} away</span>
                        </div>
                    </div>

                    <div class="flex shrink-0 flex-col items-end gap-1.5">
                        <span v-if="item.status === 'Playing'" class="animate-pulse rounded-full bg-green-500/10 px-2 py-0.5 text-xs font-bold text-green-500">LIVE</span>
                        <button
                            v-if="item.status === 'Pending' && i !== firstPendingIndex"
                            @click="bump(item)"
                            :disabled="bumpingId === item.id"
                            class="rounded-xl px-3 py-1.5 text-xs font-semibold transition active:scale-95 disabled:opacity-50"
                            style="background: linear-gradient(135deg, rgba(234, 179, 8, 0.2), rgba(234, 179, 8, 0.1)); color: #d97706; border: 1px solid rgba(234, 179, 8, 0.3)"
                        >
                            {{ bumpingId === item.id ? '⏳' : `🚀 Boost $${showStore.config.bumpCost}` }}
                        </button>
                    </div>
                </div>

                <div v-if="item.status === 'Pending' && i === 0" class="flex items-center gap-2 border-t border-yellow-500/20 bg-yellow-500/10 px-4 py-1.5">
                    <span class="animate-pulse text-xs font-bold text-yellow-500">⚡ YOU'RE UP NEXT — GET READY!</span>
                </div>
            </div>
        </div>
    </div>
</template>
