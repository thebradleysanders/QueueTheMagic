<script setup>
    import axios from 'axios';

    import { computed, ref, watch } from 'vue';

    import { useSessionStore } from '@/stores/session';
    import { useShowStore } from '@/stores/show';

    import MarqueeText from '@/components/MarqueeText.vue';

    const showStore = useShowStore();
    const sessionStore = useSessionStore();
    const np = computed(() => showStore.nowPlaying);
    const isPlaying = computed(() => np.value?.isPlaying);

    const total = computed(() => (np.value?.secondsPlayed ?? 0) + (np.value?.secondsRemaining ?? 0));
    const progress = computed(() => (total.value > 0 ? (np.value.secondsPlayed / total.value) * 100 : 0));

    function fmt(secs) {
        if (!secs) return '0:00';
        return `${Math.floor(secs / 60)}:${String(secs % 60).padStart(2, '0')}`;
    }

    // --- Rating ---
    const hovered = ref(0);
    const yourRating = ref(0);
    const avgRating = ref(0);
    const totalRatings = ref(0);
    const submitting = ref(false);
    const rated = ref(false);

    watch(
        () => np.value?.songId,
        async songId => {
            hovered.value = 0;
            yourRating.value = 0;
            avgRating.value = 0;
            totalRatings.value = 0;
            rated.value = false;

            if (!songId) return;
            try {
                const { data } = await axios.get(`/api/songs/${songId}/rating`, {
                    params: { sessionToken: sessionStore.sessionToken }
                });
                avgRating.value = data.averageRating;
                totalRatings.value = data.totalRatings;
                if (data.yourRating) {
                    yourRating.value = data.yourRating;
                    rated.value = true;
                }
            } catch {
                /* non-fatal */
            }
        },
        { immediate: true }
    );

    async function submitRating(stars) {
        if (submitting.value || !np.value?.songId) return;
        submitting.value = true;
        yourRating.value = stars;
        rated.value = true;
        try {
            const { data } = await axios.post(`/api/songs/${np.value.songId}/rate`, {
                rating: stars,
                sessionToken: sessionStore.sessionToken
            });
            avgRating.value = data.averageRating;
            totalRatings.value = data.totalRatings;
        } catch {
            rated.value = false;
            yourRating.value = 0;
        } finally {
            submitting.value = false;
        }
    }

    const displayStars = computed(() => hovered.value || yourRating.value);
</script>

<template>
    <div class="relative overflow-hidden rounded-2xl border p-4 md:p-5" :class="isPlaying ? 'np-live' : 'bg-card border-base'">
        <div class="mb-3 flex items-center justify-between">
            <span class="label flex items-center gap-2" :class="isPlaying ? '!text-[rgb(var(--ac1))]' : ''">
                <span class="h-1.5 w-1.5 rounded-full" :class="isPlaying ? 'bg-ac1 live-dot' : 'bg-[var(--bd-strong)]'" />
                {{ isPlaying ? (np.songId ? 'Now playing · requested' : 'Now playing') : 'Standby' }}
            </span>
            <div v-if="isPlaying" class="eq" aria-hidden="true">
                <span v-for="i in 5" :key="i" :style="`--c: rgb(var(--ac${(i % 3) + 1})); animation-delay: ${i * 0.13}s; animation-duration: ${0.6 + (i % 3) * 0.2}s`" />
            </div>
        </div>

        <div v-if="isPlaying">
            <MarqueeText :text="np.title" class="text-main text-xl leading-tight font-semibold tracking-tight md:text-2xl" />
            <MarqueeText v-if="np.artist" :text="np.artist" class="text-sub mt-0.5 text-sm" />

            <div v-if="total > 0" class="mt-4">
                <div class="bg-lift h-1 overflow-hidden rounded-full">
                    <div class="bg-ac1 h-full rounded-full transition-all duration-1000" :style="`width:${progress}%`" />
                </div>
                <div class="text-hint tabular mt-1.5 flex justify-between text-xs">
                    <span>{{ fmt(np.secondsPlayed) }}</span>
                    <span>-{{ fmt(np.secondsRemaining) }}</span>
                </div>
            </div>

            <div v-if="np.songId" class="border-base mt-4 flex items-center justify-between border-t pt-3" @mouseleave="hovered = 0">
                <div class="flex items-center">
                    <button
                        v-for="star in 5"
                        :key="star"
                        @mouseenter="hovered = star"
                        @click="submitRating(star)"
                        :disabled="submitting"
                        :aria-label="`Rate ${star} star${star > 1 ? 's' : ''}`"
                        class="touch-manipulation px-0.5 py-1 text-2xl leading-none transition-transform active:scale-90 disabled:opacity-50"
                        :class="star <= displayStars ? 'text-ac3' : 'text-[var(--bd-strong)]'"
                    >&#9733;</button>
                </div>
                <span class="text-hint tabular text-xs">
                    <template v-if="rated">Thanks for rating</template>
                    <template v-else>Rate this song</template>
                    <template v-if="totalRatings > 0"> · {{ avgRating }} avg ({{ totalRatings }})</template>
                </span>
            </div>
        </div>

        <p v-else class="text-sub text-sm">
            {{ np?.state === 'PlayingDefault' ? 'The default playlist is running. Request a song to jump in.' : 'The show is warming up.' }}
        </p>
    </div>
</template>

<style scoped>
    .live-dot {
        animation: live 1.6s ease-in-out infinite;
    }
    @keyframes live {
        0%, 100% { opacity: 1; }
        50% { opacity: 0.3; }
    }
    button {
        cursor: pointer;
    }
</style>
