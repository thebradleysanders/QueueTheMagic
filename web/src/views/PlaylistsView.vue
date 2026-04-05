<script setup>
import { ref, onMounted } from 'vue'
import axios from 'axios'
import MarqueeText from '@/components/MarqueeText.vue'

const playlists = ref([])
const open = ref({})

onMounted(async () => {
  const { data } = await axios.get('/api/songs')
  playlists.value = data
  if (data.length) open.value[data[0].id] = true
})

function toggle(id) {
  open.value[id] = !open.value[id]
}

function formatDuration(secs) {
  if (!secs) return ''
  return `${Math.floor(secs / 60)}:${String(secs % 60).padStart(2, '0')}`
}
</script>

<template>
  <main class="max-w-2xl md:max-w-3xl mx-auto px-4 md:px-8 py-8">
    <h1 class="text-2xl font-bold text-main mb-6">Song Library</h1>

    <div v-if="playlists.length === 0" class="text-hint text-center py-12">
      No songs available. Admin: run sync-songs to import from FPP.
    </div>

    <div v-for="playlist in playlists" :key="playlist.id" class="mb-3">
      <button
        @click="toggle(playlist.id)"
        class="w-full flex items-center justify-between bg-card rounded-xl px-5 py-4 border border-base hover:border-strong transition"
      >
        <div class="text-left">
          <p class="font-semibold text-main">{{ playlist.name }}</p>
          <p class="text-xs text-hint mt-0.5">{{ playlist.songs.length }} songs</p>
        </div>
        <i :class="`pi pi-chevron-${open[playlist.id] ? 'up' : 'down'} text-hint`"></i>
      </button>

      <div v-if="open[playlist.id]" class="mt-1 border border-base rounded-xl overflow-hidden">
        <div
          v-for="song in playlist.songs"
          :key="song.id"
          class="flex items-center px-5 py-3 border-b border-base last:border-0 bg-card hover:bg-lift"
        >
          <div class="flex-1 min-w-0">
            <MarqueeText :text="song.title" class="text-sm font-medium text-main" />
            <p class="text-xs text-hint" v-if="song.artist">{{ song.artist }}</p>
          </div>
          <span v-if="song.totalRatings > 0" class="shrink-0 flex items-center gap-0.5 text-xs text-yellow-500 font-semibold mx-2">
            ★ {{ song.averageRating }}
          </span>
          <span class="text-xs text-ghost ml-2">{{ formatDuration(song.durationSeconds) }}</span>
        </div>
      </div>
    </div>
  </main>
</template>
