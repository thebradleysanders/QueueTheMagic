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
    const isDark = computed(() => showStore.config.theme !== 'light');

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

    const cardStyle = computed(() => {
        if (!isPlaying.value) return {};
        return isDark.value ? { background: 'linear-gradient(135deg, #052e16 0%, #111827 60%, #111827 100%)' } : { background: 'linear-gradient(135deg, #dcfce7 0%, #ffffff 60%, #ffffff 100%)' };
    });
</script>

<template>
    <div class="relative overflow-hidden rounded-2xl border p-4 md:p-6" :class="isPlaying ? 'border-green-500/30' : 'border-base bg-card'" :style="isPlaying ? cardStyle : {}">
        <!-- Glow blob when playing -->
        <div v-if="isPlaying" class="pointer-events-none absolute -top-8 -right-8 h-32 w-32 rounded-full bg-green-400 opacity-20 blur-2xl" />
        <div v-else class="pointer-events-none absolute -bottom-8 -left-8 h-32 w-32 rounded-full bg-white/40 opacity-20 blur-2xl" />

        <!-- Header row -->
        <div class="relative mb-3 flex items-center justify-between">
            <div class="flex items-center gap-2">
                <span class="text-xs tracking-widest uppercase font-bold" :class="isPlaying ? 'text-green-500' : 'text-hint'">
                    <template v-if="isPlaying">
                        <font-awesome-icon :icon="['fas', 'music']" />
                        Now Playing
                    </template>
                    <template v-else>
                        <font-awesome-icon :icon="['fas', 'snowflake']" />
                        Standby Mode
                    </template>
                </span>
            </div>
            <div v-if="isPlaying" class="flex h-5 items-end gap-0.5">
                <span v-for="i in 5" :key="i" class="w-1 rounded-full bg-green-500" :style="`height:${6 + i * 3}px; animation: bounce${i} 0.8s ease-in-out infinite alternate; animation-delay:${i * 0.12}s`" />
            </div>
        </div>

        <!-- Track info -->
        <div v-if="isPlaying" class="relative">
            <MarqueeText :text="np.title" class="text-main text-xl leading-tight font-bold md:text-3xl" />
            <MarqueeText v-if="np.artist" :text="np.artist" class="text-sub mt-0.5 text-sm md:text-base" />

            <!-- Progress -->
            <div v-if="total > 0" class="mt-3">
                <div class="bg-lift h-1.5 overflow-hidden rounded-full">
                    <div class="h-full rounded-full transition-all duration-1000" style="background: linear-gradient(90deg, #22c55e, #4ade80)" :style="`width:${progress}%`" />
                </div>
                <div class="text-hint mt-1.5 flex justify-between text-xs">
                    <span>{{ fmt(np.secondsPlayed) }}</span>
                    <span>{{ fmt(np.secondsRemaining) }} left</span>
                </div>
            </div>

            <!-- Star rating -->
            <div v-if="np.songId" class="border-base mt-3 border-t pt-3">
                <div class="flex items-center justify-between">
                    <span class="text-hint text-xs">{{ rated ? 'Your rating' : 'Rate this song' }}</span>
                    <span v-if="totalRatings > 0" class="text-hint text-xs">★ {{ avgRating }} avg · {{ totalRatings }} {{ totalRatings === 1 ? 'vote' : 'votes' }}</span>
                </div>
                <div class="mt-2 flex gap-0.5" @mouseleave="hovered = 0">
                    <button
                        v-for="star in 5"
                        :key="star"
                        @mouseenter="hovered = star"
                        @click="submitRating(star)"
                        :disabled="submitting"
                        class="flex-1 touch-manipulation py-2 text-center text-4xl leading-none transition-transform active:scale-90 disabled:opacity-50"
                        :class="star <= displayStars ? 'opacity-100' : 'opacity-20'"
                        style="min-width: 44px"
                    >
                        {{ star <= displayStars ? '★' : '☆' }}
                    </button>
                </div>
            </div>
        </div>

        <!-- Idle state -->
        <div v-else>
            <p class="text-hint text-sm" v-if="np?.state === 'PlayingDefault'">
                Default playlist running
            </p>
            <p v-else class="text-hint font-semibold text-sm">
              Warming up the lights...
            </p>
        </div>
    </div>
</template>

<style scoped>
    @keyframes bounce1 {
        from {
            height: 6px;
        }
        to {
            height: 18px;
        }
    }
    @keyframes bounce2 {
        from {
            height: 9px;
        }
        to {
            height: 14px;
        }
    }
    @keyframes bounce3 {
        from {
            height: 12px;
        }
        to {
            height: 20px;
        }
    }
    @keyframes bounce4 {
        from {
            height: 7px;
        }
        to {
            height: 16px;
        }
    }
    @keyframes bounce5 {
        from {
            height: 10px;
        }
        to {
            height: 22px;
        }
    }
    button {
        cursor: pointer;
    }
</style>
