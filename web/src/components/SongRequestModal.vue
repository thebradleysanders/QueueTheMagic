<script setup>
import { ref, onMounted, computed, nextTick } from 'vue'
import axios from 'axios'
import { useShowStore } from '@/stores/show'
import { useSessionStore } from '@/stores/session'
import { usePayment } from '@/composables/usePayment'
import MarqueeText from '@/components/MarqueeText.vue'

const emit = defineEmits(['close'])
const showStore = useShowStore()
const sessionStore = useSessionStore()
const { createIntent, mountPaymentElement, confirmPayment } = usePayment()
const isDark = computed(() => showStore.config.theme !== 'light')

const playlists = ref([])
const selectedPlaylist = ref(null)
const selectedSong = ref(null)
const step = ref('select')
const errorMsg = ref(null)
const loading = ref(false)
const paymentElementRef = ref(null)

onMounted(async () => {
  const { data } = await axios.get('/api/songs')
  playlists.value = data
})

const isDev = import.meta.env.DEV

function pickPlaylist(p) {
  selectedPlaylist.value = p
  selectedSong.value = null
}

function goBack() {
  selectedPlaylist.value = null
  selectedSong.value = null
}

async function pay() {
  if (!selectedSong.value) return
  loading.value = true
  errorMsg.value = null
  try {
    if (isDev) {
      await axios.post('/api/queue', {
        songId: selectedSong.value.id,
        paymentIntentId: 'dev_test',
        sessionToken: sessionStore.sessionToken,
      })
      await showStore.fetchQueue()
      step.value = 'done'
      return
    }
    const clientSecret = await createIntent('song', selectedSong.value.id)
    step.value = 'payment'
    await nextTick()
    await mountPaymentElement(clientSecret, paymentElementRef.value)
  } catch (e) {
    errorMsg.value = e.response?.data?.error || e.message || 'Something went wrong.'
    step.value = 'select'
  } finally {
    loading.value = false
  }
}

async function submitPayment() {
  loading.value = true
  errorMsg.value = null
  try {
    const paymentIntentId = await confirmPayment(window.location.href)
    await axios.post('/api/queue', {
      songId: selectedSong.value.id,
      paymentIntentId,
      sessionToken: sessionStore.sessionToken,
    })
    await showStore.fetchQueue()
    step.value = 'done'
  } catch (e) {
    errorMsg.value = e.response?.data?.error || e.message || 'Something went wrong.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4" @click.self="emit('close')">
    <div class="bg-card rounded-2xl w-full max-w-lg md:max-w-2xl border border-base overflow-hidden">

      <!-- Header -->
      <div class="flex items-center justify-between px-5 py-4 border-b border-base">
        <div class="flex items-center gap-2">
          <button v-if="selectedPlaylist && step === 'select'" @click="goBack"
            class="text-hint hover:text-main p-1 -ml-1">←</button>
          <div>
            <h2 class="text-base font-bold text-main leading-tight">
              {{ selectedPlaylist ? selectedPlaylist.name : '🎵 Pick a Song' }}
            </h2>
            <p v-if="!selectedPlaylist" class="text-xs text-hint">${{ showStore.config.songRequestCost }} to request</p>
          </div>
        </div>
        <button @click="emit('close')" class="text-hint hover:text-main text-xl w-8 h-8 flex items-center justify-center">✕</button>
      </div>

      <!-- Step: select playlist -->
      <div v-if="step === 'select' && !selectedPlaylist" class="p-4 max-h-[65vh] overflow-y-auto space-y-2">
        <button
          v-for="p in playlists"
          :key="p.id"
          @click="pickPlaylist(p)"
          class="w-full text-left px-4 py-4 rounded-2xl bg-lift hover:bg-inset active:scale-98 transition border border-base hover:border-strong flex items-center gap-3"
        >
          <span class="text-2xl">🎄</span>
          <div>
            <p class="font-semibold text-main">{{ p.name }}</p>
            <p class="text-xs text-hint mt-0.5">{{ p.songs.length }} song{{ p.songs.length !== 1 ? 's' : '' }}</p>
          </div>
          <span class="ml-auto text-hint text-lg">›</span>
        </button>
      </div>

      <!-- Step: select song -->
      <div v-if="step === 'select' && selectedPlaylist" class="p-4 max-h-[65vh] overflow-y-auto">
        <div v-if="errorMsg" class="mb-3 p-3 bg-red-500/20 text-red-500 rounded-xl text-sm">{{ errorMsg }}</div>
        <div class="space-y-1.5">
          <button
            v-for="song in selectedPlaylist.songs"
            :key="song.id"
            @click="selectedSong = song"
            class="w-full text-left px-4 py-3 rounded-xl text-sm transition active:scale-98 border"
            :class="selectedSong?.id === song.id
              ? 'bg-green-500/15 border-green-500/40 text-main'
              : 'bg-lift border-base text-sub hover:bg-inset'"
          >
            <div class="flex items-center gap-2">
              <span v-if="selectedSong?.id === song.id" class="text-green-500 text-base shrink-0">✓</span>
              <span v-else class="text-hint text-base shrink-0">○</span>
              <MarqueeText :text="song.title" class="font-medium flex-1 text-main" />
              <span v-if="song.totalRatings > 0" class="shrink-0 flex items-center gap-0.5 text-xs text-yellow-500 font-semibold">
                ★ {{ song.averageRating }}
              </span>
              <span v-if="song.durationSeconds" class="text-hint text-xs shrink-0">
                {{ Math.floor(song.durationSeconds / 60) }}:{{ String(song.durationSeconds % 60).padStart(2, '0') }}
              </span>
            </div>
            <p v-if="song.artist" class="text-xs text-hint mt-0.5 ml-6 truncate">{{ song.artist }}</p>
          </button>
        </div>
      </div>

      <!-- Step: payment form -->
      <div v-if="step === 'payment'" class="p-5">
        <div v-if="errorMsg" class="mb-3 p-3 bg-red-500/20 text-red-400 rounded-xl text-sm">{{ errorMsg }}</div>
        <div ref="paymentElementRef"></div>
        <button
          @click="submitPayment"
          :disabled="loading"
          class="w-full mt-4 py-4 font-bold text-base text-white rounded-2xl transition active:scale-95 disabled:opacity-40"
          style="background: linear-gradient(135deg, #16a34a 0%, #15803d 100%)"
        >
          {{ loading ? '⏳ Processing...' : `🎵 Pay $${showStore.config.songRequestCost}` }}
        </button>
      </div>

      <!-- Step: paying -->
      <div v-if="step === 'paying'" class="p-8 text-center">
        <div class="text-4xl mb-3 animate-spin">💫</div>
        <p class="text-sub font-medium">Processing payment...</p>
      </div>

      <!-- Step: done -->
      <div v-if="step === 'done'" class="p-8 text-center">
        <div class="text-5xl mb-4">🎉</div>
        <p class="text-xl font-bold text-main">You're in the queue!</p>
        <p class="text-sm text-hint mt-1 mb-1">{{ selectedSong?.title }}</p>
        <p class="text-xs text-ghost">Watch the queue to see when you're up next</p>
        <button @click="emit('close')"
          class="mt-5 px-8 py-3 text-white font-semibold rounded-2xl text-sm transition active:scale-95"
          style="background: linear-gradient(135deg, #16a34a, #15803d)">
          Awesome! 🎄
        </button>
      </div>

      <!-- Pay button -->
      <div v-if="step === 'select' && selectedPlaylist" class="px-4 pb-5 pt-2">
        <button
          @click="pay"
          :disabled="!selectedSong || loading"
          class="w-full py-4 font-bold text-base text-white rounded-2xl transition active:scale-95 disabled:opacity-40"
          style="background: linear-gradient(135deg, #16a34a 0%, #15803d 100%)"
        >
          {{ loading ? '⏳ Processing...' : selectedSong ? (isDev ? `⚡ Queue "${selectedSong.title}" (dev)` : `🎵 Queue Song — $${showStore.config.songRequestCost}`) : 'Select a song above' }}
        </button>
      </div>

    </div>
  </div>
</template>
