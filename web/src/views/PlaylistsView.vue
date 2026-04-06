<script setup>
    import axios from 'axios';

    import { onMounted, ref } from 'vue';

    import MarqueeText from '@/components/MarqueeText.vue';

    const playlists = ref([]);
    const open = ref({});

    onMounted(async () => {
        const { data } = await axios.get('/api/songs');
        playlists.value = data;
        if (data.length) open.value[data[0].id] = true;
    });

    function toggle(id) {
        open.value[id] = !open.value[id];
    }

    function formatDuration(secs) {
        if (!secs) return '';
        return `${Math.floor(secs / 60)}:${String(secs % 60).padStart(2, '0')}`;
    }
</script>

<template>
    <main class="mx-auto max-w-2xl px-4 py-8 md:max-w-3xl md:px-8">
        <h1 class="text-main mb-6 text-2xl font-bold">Song Library</h1>

        <div v-if="playlists.length === 0" class="text-hint py-12 text-center">No songs available. Admin: run sync-songs to import from FPP.</div>

        <div v-for="playlist in playlists" :key="playlist.id" class="mb-3">
            <button @click="toggle(playlist.id)" class="bg-card border-base hover:border-strong flex w-full items-center justify-between rounded-xl border px-5 py-4 transition">
                <div class="text-left">
                    <p class="text-main font-semibold">{{ playlist.name }}</p>
                    <p class="text-hint mt-0.5 text-xs">{{ playlist.songs.length }} songs</p>
                </div>
                <i :class="`pi pi-chevron-${open[playlist.id] ? 'up' : 'down'} text-hint`"></i>
            </button>

            <div v-if="open[playlist.id]" class="border-base mt-1 overflow-hidden rounded-xl border">
                <div v-for="song in playlist.songs" :key="song.id" class="border-base bg-card hover:bg-lift flex items-center border-b px-5 py-3 last:border-0">
                    <div class="min-w-0 flex-1">
                        <MarqueeText :text="song.title" class="text-main text-sm font-medium" />
                        <p class="text-hint text-xs" v-if="song.artist">{{ song.artist }}</p>
                    </div>
                    <span v-if="song.totalRatings > 0" class="mx-2 flex shrink-0 items-center gap-0.5 text-xs font-semibold text-yellow-500">★ {{ song.averageRating }}</span>
                    <span class="text-ghost ml-2 text-xs">{{ formatDuration(song.durationSeconds) }}</span>
                </div>
            </div>
        </div>
    </main>
</template>
