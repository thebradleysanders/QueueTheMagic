<script setup>
import { computed, ref, watch } from 'vue'
import { useShowStore } from '@/stores/show'
import { useSessionStore } from '@/stores/session'
import axios from 'axios'
import MarqueeText from '@/components/MarqueeText.vue'

const showStore = useShowStore()
const sessionStore = useSessionStore()
const np = computed(() => showStore.nowPlaying)
const isPlaying = computed(() => np.value?.isPlaying)
const isDark = computed(() => showStore.config.theme !== 'light')

const total = computed(() => (np.value?.secondsPlayed ?? 0) + (np.value?.secondsRemaining ?? 0))
const progress = computed(() => total.value > 0 ? (np.value.secondsPlayed / total.value) * 100 : 0)

function fmt(secs) {
  if (!secs) return '0:00'
  return `${Math.floor(secs / 60)}:${String(secs % 60).padStart(2, '0')}`
}

// --- Rating ---
const hovered = ref(0)
const yourRating = ref(0)
const avgRating = ref(0)
const totalRatings = ref(0)
const submitting = ref(false)
const rated = ref(false)

watch(() => np.value?.songId, async (songId) => {
  hovered.value = 0
  yourRating.value = 0
  avgRating.value = 0
  totalRatings.value = 0
  rated.value = false

  if (!songId) return
  try {
    const { data } = await axios.get(`/api/songs/${songId}/rating`, {
      params: { sessionToken: sessionStore.sessionToken }
    })
    avgRating.value = data.averageRating
    totalRatings.value = data.totalRatings
    if (data.yourRating) {
      yourRating.value = data.yourRating
      rated.value = true
    }
  } catch { /* non-fatal */ }
}, { immediate: true })

async function submitRating(stars) {
  if (submitting.value || !np.value?.songId) return
  submitting.value = true
  yourRating.value = stars
  rated.value = true
  try {
    const { data } = await axios.post(`/api/songs/${np.value.songId}/rate`, {
      rating: stars,
      sessionToken: sessionStore.sessionToken,
    })
    avgRating.value = data.averageRating
    totalRatings.value = data.totalRatings
  } catch {
    rated.value = false
    yourRating.value = 0
  } finally {
    submitting.value = false
  }
}

const displayStars = computed(() => hovered.value || yourRating.value)

const cardStyle = computed(() => {
  if (!isPlaying.value) return {}
  return isDark.value
    ? { background: 'linear-gradient(135deg, #052e16 0%, #111827 60%, #111827 100%)' }
    : { background: 'linear-gradient(135deg, #dcfce7 0%, #ffffff 60%, #ffffff 100%)' }
})
</script>

<template>
  <div class="rounded-2xl p-4 md:p-6 relative overflow-hidden border"
    :class="isPlaying ? 'border-green-500/30' : 'border-base bg-card'"
    :style="isPlaying ? cardStyle : {}">

    <!-- Glow blob when playing -->
    <div v-if="isPlaying"
      class="absolute -top-8 -right-8 w-32 h-32 rounded-full opacity-20 blur-2xl bg-green-400 pointer-events-none" />

    <!-- Header row -->
    <div class="flex items-center justify-between mb-3 relative">
      <div class="flex items-center gap-2">
        <span class="text-xs uppercase tracking-widest font-semibold"
          :class="isPlaying ? 'text-green-500' : 'text-hint'">
          {{ isPlaying ? '🎵 Now Playing' : '⏸ Idle' }}
        </span>
      </div>
      <div v-if="isPlaying" class="flex gap-0.5 items-end h-5">
        <span v-for="i in 5" :key="i"
          class="w-1 bg-green-500 rounded-full"
          :style="`height:${6 + i*3}px; animation: bounce${i} 0.8s ease-in-out infinite alternate; animation-delay:${i*0.12}s`" />
      </div>
    </div>

    <!-- Track info -->
    <div v-if="isPlaying" class="relative">
      <MarqueeText :text="np.title" class="text-xl md:text-3xl font-bold text-main leading-tight" />
      <MarqueeText v-if="np.artist" :text="np.artist" class="text-sm md:text-base text-sub mt-0.5" />

      <!-- Progress -->
      <div v-if="total > 0" class="mt-3">
        <div class="h-1.5 bg-lift rounded-full overflow-hidden">
          <div class="h-full rounded-full transition-all duration-1000"
            style="background: linear-gradient(90deg, #22c55e, #4ade80)"
            :style="`width:${progress}%`" />
        </div>
        <div class="flex justify-between text-xs text-hint mt-1.5">
          <span>{{ fmt(np.secondsPlayed) }}</span>
          <span>{{ fmt(np.secondsRemaining) }} left</span>
        </div>
      </div>

      <!-- Star rating -->
      <div v-if="np.songId" class="mt-3 pt-3 border-t border-base">
        <div class="flex items-center justify-between">
          <span class="text-xs text-hint">{{ rated ? 'Your rating' : 'Rate this song' }}</span>
          <span v-if="totalRatings > 0" class="text-xs text-hint">
            ★ {{ avgRating }} avg · {{ totalRatings }} {{ totalRatings === 1 ? 'vote' : 'votes' }}
          </span>
        </div>
        <div class="flex gap-0.5 mt-2" @mouseleave="hovered = 0">
          <button
            v-for="star in 5"
            :key="star"
            @mouseenter="hovered = star"
            @click="submitRating(star)"
            :disabled="submitting"
            class="flex-1 py-2 text-4xl transition-transform active:scale-90 disabled:opacity-50 leading-none text-center touch-manipulation"
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
      <p class="text-hint text-sm">
        {{ np?.state === 'PlayingDefault' ? '🎄 Default playlist running' : 'Warming up the lights...' }}
      </p>
    </div>
  </div>
</template>

<style scoped>
@keyframes bounce1 { from { height: 6px } to { height: 18px } }
@keyframes bounce2 { from { height: 9px } to { height: 14px } }
@keyframes bounce3 { from { height: 12px } to { height: 20px } }
@keyframes bounce4 { from { height: 7px } to { height: 16px } }
@keyframes bounce5 { from { height: 10px } to { height: 22px } }
button { cursor: pointer; }
</style>
