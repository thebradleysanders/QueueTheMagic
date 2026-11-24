<script setup>
    import SupporterNameField from '@/components/SupporterNameField.vue';
    import { useSupporterName } from '@/composables/useSupporterName';
    import Celebrate from '@/components/Celebrate.vue';
    import axios from 'axios';

    import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue';

    import { usePayment } from '@/composables/usePayment';

    import { useSessionStore } from '@/stores/session';
    import { useShowStore } from '@/stores/show';

    import MarqueeText from '@/components/MarqueeText.vue';

    const emit = defineEmits(['close']);
    const showStore = useShowStore();
    const sessionStore = useSessionStore();
    const { displayName } = useSupporterName();
    const { createIntent, mountPaymentElement, confirmPayment } = usePayment();

    // How long until a song requested right now would play.
    const songsAhead = computed(() => showStore.queue.length);
    const waitSeconds = computed(() => {
        let secs = 0;
        for (const q of showStore.queue) {
            if (q.status === 'Playing') secs += showStore.nowPlaying?.secondsRemaining || q.durationSeconds || 0;
            else secs += q.durationSeconds || 0;
        }
        return secs;
    });
    const waitMinutes = computed(() => Math.max(1, Math.round(waitSeconds.value / 60)));
    const longWait = computed(() => waitSeconds.value >= 15 * 60);

    const playlists = ref([]);
    const selectedPlaylist = ref(null);
    const selectedSong = ref(null);
    const step = ref('select');
    const errorMsg = ref(null);
    const loading = ref(false);
    const paymentElementRef = ref(null);

    function onKey(e) {
        if (e.key === 'Escape' && !loading.value) emit('close');
    }

    onMounted(async () => {
        window.addEventListener('keydown', onKey);
        try {
            const { data } = await axios.get('/api/songs');
            playlists.value = data;
        } catch {
            errorMsg.value = 'Could not load the song list. Please try again.';
        }
    });
    onUnmounted(() => window.removeEventListener('keydown', onKey));

    const isDev = import.meta.env.DEV;

    function pickPlaylist(p) {
        selectedPlaylist.value = p;
        selectedSong.value = null;
    }

    function goBack() {
        selectedPlaylist.value = null;
        selectedSong.value = null;
    }

    async function pay() {
        if (!selectedSong.value) return;
        loading.value = true;
        errorMsg.value = null;
        try {
            if (isDev) {
                await axios.post('/api/queue', {
                    songId: selectedSong.value.id,
                    paymentIntentId: 'dev_test',
                    sessionToken: sessionStore.sessionToken,
                    displayName: displayName.value
                });
                await showStore.fetchQueue();
                step.value = 'done';
                return;
            }
            const clientSecret = await createIntent('song', selectedSong.value.id);
            step.value = 'payment';
            await nextTick();
            await mountPaymentElement(clientSecret, paymentElementRef.value);
        } catch (e) {
            errorMsg.value = e.response?.data?.error || e.message || 'Something went wrong.';
            step.value = 'select';
        } finally {
            loading.value = false;
        }
    }

    async function submitPayment() {
        loading.value = true;
        errorMsg.value = null;
        try {
            const paymentIntentId = await confirmPayment(window.location.href);
            await axios.post('/api/queue', {
                songId: selectedSong.value.id,
                paymentIntentId,
                sessionToken: sessionStore.sessionToken,
                displayName: displayName.value
            });
            await showStore.fetchQueue();
            step.value = 'done';
        } catch (e) {
            errorMsg.value = e.response?.data?.error || e.message || 'Something went wrong.';
        } finally {
            loading.value = false;
        }
    }
</script>

<template>
    <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4" role="dialog" aria-modal="true" aria-label="Request a song" @click.self="emit('close')">
        <div class="bg-card border-base w-full max-w-lg overflow-hidden rounded-xl border md:max-w-2xl">
            <!-- Header -->
            <div class="border-base flex items-center justify-between border-b px-5 py-4">
                <div class="flex items-center gap-2">
                    <button v-if="selectedPlaylist && step === 'select'" @click="goBack" aria-label="Back" class="text-hint hover:text-main -ml-1 p-1">
                        <font-awesome-icon :icon="['fas', 'chevron-left']" />
                    </button>
                    <div>
                        <h2 class="text-main text-base leading-tight font-semibold">
                            <font-awesome-icon v-if="!selectedPlaylist" :icon="['fas', 'music']" />
                            {{ selectedPlaylist ? selectedPlaylist.name : 'Pick a Song' }}
                        </h2>
                        <p v-if="!selectedPlaylist" class="text-hint text-xs">${{ showStore.config.songRequestCost }} to request</p>
                    </div>
                </div>
                <button @click="emit('close')" aria-label="Close" class="text-hint hover:text-main flex h-8 w-8 items-center justify-center text-xl">
                    <font-awesome-icon :icon="['fas', 'xmark']" />
                </button>
            </div>

            <!-- Queue wait estimate -->
            <div v-if="step === 'select'" class="border-base border-b px-5 py-2.5 text-xs" :class="longWait ? 'bg-ac3-soft text-ac3' : 'text-sub'">
                <template v-if="songsAhead === 0">The queue is empty. Your song plays next.</template>
                <template v-else>
                    {{ songsAhead }} {{ songsAhead === 1 ? 'song' : 'songs' }} ahead of you · about {{ waitMinutes }} min wait
                    <span v-if="longWait" class="block opacity-80">Busy night. You can boost your song up the queue after requesting it.</span>
                </template>
            </div>

            <!-- Step: select playlist -->
            <div v-if="step === 'select' && !selectedPlaylist" class="max-h-[65vh] space-y-2 overflow-y-auto p-4">
                <div v-if="errorMsg" class="mb-1 rounded-xl bg-red-500/20 p-3 text-sm text-red-500">{{ errorMsg }}</div>
                <button v-for="p in playlists" :key="p.id" @click="pickPlaylist(p)" class="bg-lift hover:bg-inset border-base hover:border-strong flex w-full items-center gap-3 rounded-xl border px-4 py-4 text-left transition active:scale-98">
                    <font-awesome-icon v-if="p.songs.length === 0" :icon="['fas', 'exclamation-triangle']" class="text-yellow-500" />
                    <font-awesome-icon v-else :icon="['fas', 'music']" />
                    <div>
                        <p class="text-main font-semibold">{{ p.name }}</p>
                        <p class="text-hint mt-0.5 text-xs">{{ p.songs.length }} song{{ p.songs.length !== 1 ? 's' : '' }}</p>
                    </div>
                    <span class="text-hint ml-auto text-lg">
                        <font-awesome-icon :icon="['fas', 'chevron-right']" />
                    </span>
                </button>
            </div>

            <!-- Step: select song -->
            <div v-if="step === 'select' && selectedPlaylist" class="max-h-[65vh] overflow-y-auto p-4">
                <div v-if="errorMsg" class="mb-3 rounded-xl bg-red-500/20 p-3 text-sm text-red-500">{{ errorMsg }}</div>
                <div class="space-y-1.5">
                    <button
                        v-for="song in selectedPlaylist.songs"
                        :key="song.id"
                        @click="selectedSong = song"
                        class="w-full rounded-xl border px-4 py-3 text-left text-sm transition active:scale-98"
                        :class="selectedSong?.id === song.id ? 'text-main border-ac1-soft bg-ac1-soft' : 'bg-lift border-base text-sub hover:bg-inset'"
                    >
                        <div class="flex items-center gap-2">
                            <span v-if="selectedSong?.id === song.id" class="text-ac1 shrink-0 text-base">✓</span>
                            <span v-else class="text-hint shrink-0 text-base">○</span>
                            <MarqueeText :text="song.title" class="text-main flex-1 font-medium" />
                            <span v-if="song.totalRatings > 0" class="flex shrink-0 items-center gap-0.5 text-ac3 text-xs font-semibold">★ {{ song.averageRating }}</span>
                            <span v-if="song.durationSeconds" class="text-hint shrink-0 text-xs">{{ Math.floor(song.durationSeconds / 60) }}:{{ String(song.durationSeconds % 60).padStart(2, '0') }}</span>
                        </div>
                        <p v-if="song.artist" class="text-hint mt-0.5 ml-6 truncate text-xs">{{ song.artist }}</p>
                    </button>
                </div>
            </div>

            <!-- Step: payment form -->
            <div v-if="step === 'payment'" class="p-5">
                <div v-if="errorMsg" class="mb-3 rounded-xl bg-red-500/20 p-3 text-sm text-red-400">{{ errorMsg }}</div>
                <div ref="paymentElementRef"></div>
                <button @click="submitPayment" :disabled="loading" class="btn-ac mt-4 w-full rounded-xl py-4 text-base font-semibold transition active:scale-95 disabled:opacity-40">
                    {{ loading ? 'Processing…' : `Pay $${showStore.config.songRequestCost}` }}
                </button>
            </div>

            <!-- Step: paying -->
            <div v-if="step === 'paying'" class="p-8 text-center">
                <div class="border-strong border-t-ac1 mx-auto mb-4 h-8 w-8 animate-spin rounded-full border-2" style="border-top-color: rgb(var(--ac1))"></div>
                <p class="text-sub font-medium">Processing payment...</p>
            </div>

            <!-- Step: done -->
            <div v-if="step === 'done'" class="p-8 text-center">
                <Celebrate :count="28" :spread="140"><font-awesome-icon :icon="['fas', 'check']" class="text-ac1 mb-4 text-3xl" /></Celebrate>
                <p class="text-main text-xl font-semibold">You're in the queue!</p>
                <p class="text-hint mt-1 mb-1 text-sm">{{ selectedSong?.title }}</p>
                <p class="text-ghost text-xs">Watch the queue to see when you're up next</p>
                <button @click="emit('close')" class="btn-ac mt-5 rounded-xl px-8 py-3 text-sm font-semibold transition active:scale-95">Done</button>
            </div>

            <!-- Pay button -->
            <div v-if="step === 'select' && selectedPlaylist" class="space-y-3 px-4 pt-2 pb-5">
                <SupporterNameField v-if="selectedSong" />
                <button @click="pay" :disabled="!selectedSong || loading" class="btn-ac w-full rounded-xl py-4 text-base font-semibold transition active:scale-95 disabled:opacity-40">
                    {{ loading ? 'Processing…' : selectedSong ? (isDev ? `Queue "${selectedSong.title}" (dev)` : `Request song · $${showStore.config.songRequestCost}`) : 'Select a song above' }}
                </button>
            </div>
        </div>
    </div>
</template>
