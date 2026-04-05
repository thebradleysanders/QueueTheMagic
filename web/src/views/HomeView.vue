<script setup>
import { ref, computed } from 'vue'
import { useShowStore } from '@/stores/show'
import { useSessionStore } from '@/stores/session'
import { usePayment } from '@/composables/usePayment'
import axios from 'axios'
import NowPlayingCard from '@/components/NowPlayingCard.vue'
import MarqueeText from '@/components/MarqueeText.vue'
import SongRequestModal from '@/components/SongRequestModal.vue'
import DonateModal from '@/components/DonateModal.vue'

const showStore = useShowStore()
const sessionStore = useSessionStore()
const { createIntent, confirmPayment } = usePayment()
const showModal = ref(false)
const showDonate = ref(false)
const bumpingId = ref(null)
const isDev = import.meta.env.DEV
const isDark = computed(() => showStore.config.theme !== 'light')
const isOpen = computed(() => showStore.config.isOpen)
const queueDepth = computed(() => showStore.queue.filter(q => q.status === 'Pending').length)

function formatDuration(seconds) {
  if (!seconds) return ''
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return m > 0 ? `${m}m ${s > 0 ? s + 's' : ''}`.trim() : `${s}s`
}

function estimatedWaitSeconds(itemIndex) {
  const items = showStore.queue
  let secs = 0
  if (items[0]?.status === 'Playing') secs += showStore.nowPlaying?.secondsRemaining || 0
  for (let i = 0; i < itemIndex; i++) {
    if (items[i].status === 'Pending') secs += items[i].durationSeconds || 0
  }
  return secs
}

function formatWait(secs) {
  if (!secs) return null
  if (secs < 60) return `~${secs}s`
  return `~${Math.round(secs / 60)} min`
}

async function boost(item) {
  if (bumpingId.value) return
  bumpingId.value = item.id
  try {
    let paymentIntentId
    if (isDev) {
      paymentIntentId = 'dev_test'
    } else {
      const clientSecret = await createIntent('bump', item.id)
      paymentIntentId = await confirmPayment(clientSecret, window.location.href)
    }
    await axios.put(`/api/queue/${item.id}/bump`, {
      paymentIntentId,
      sessionToken: sessionStore.sessionToken,
    })
    await showStore.fetchQueue()
  } catch (e) {
    alert(e.message || 'Boost failed')
  } finally {
    bumpingId.value = null
  }
}

const radioStyle = computed(() => isDark.value
  ? 'background: linear-gradient(135deg, #0f172a 0%, #1a1040 50%, #0f172a 100%); border: 1px solid rgba(139,92,246,0.35); box-shadow: 0 0 32px rgba(139,92,246,0.15)'
  : 'background: linear-gradient(135deg, #f5f3ff 0%, #ede9fe 50%, #f5f3ff 100%); border: 1px solid rgba(139,92,246,0.3); box-shadow: 0 2px 16px rgba(139,92,246,0.1)')

const radioGlowStyle = computed(() => isDark.value
  ? 'background: radial-gradient(ellipse at 50% 0%, rgba(139,92,246,0.2) 0%, transparent 70%)'
  : 'background: radial-gradient(ellipse at 50% 0%, rgba(139,92,246,0.1) 0%, transparent 70%)')

const radioIconStyle = computed(() => isDark.value
  ? 'background: rgba(139,92,246,0.2); border: 1px solid rgba(139,92,246,0.3)'
  : 'background: rgba(139,92,246,0.15); border: 1px solid rgba(139,92,246,0.25)')

const radioLabelColor = computed(() => isDark.value ? 'rgba(167,139,250,0.8)' : 'rgba(109,40,217,0.8)')
const radioSubColor = computed(() => isDark.value ? 'rgba(167,139,250,0.6)' : 'rgba(109,40,217,0.6)')
const radioStationColor = computed(() => isDark.value ? '#ffffff' : '#3b0764')
</script>

<template>
  <div class="max-w-lg md:max-w-2xl mx-auto px-4 md:px-8 pt-4 pb-6">

    <!-- FM Radio banner -->
    <div v-if="showStore.config.fmRadioStation"
      class="relative mb-5 rounded-2xl overflow-hidden"
      :style="radioStyle">
      <div class="absolute inset-0 pointer-events-none" :style="radioGlowStyle" />
      <div class="relative flex items-center gap-4 px-5 py-4">
        <div class="shrink-0 w-12 h-12 rounded-xl flex items-center justify-center text-2xl"
          :style="radioIconStyle">
          📻
        </div>
        <div class="flex-1 min-w-0">
          <p class="text-xs font-semibold uppercase tracking-widest mb-0.5" :style="`color: ${radioLabelColor}`">
            Tune your radio to
          </p>
          <p class="text-2xl md:text-4xl font-black leading-none tracking-tight" :style="`color: ${radioStationColor}`">
            {{ showStore.config.fmRadioStation }}
          </p>
          <p class="text-xs mt-1" :style="`color: ${radioSubColor}`">to hear the show live</p>
        </div>
        <div class="shrink-0 flex items-center gap-0.5 pr-1">
          <span v-for="i in 4" :key="i"
            class="rounded-full"
            :style="`width: 3px; height: ${6 + i * 4}px; opacity: ${0.3 + i * 0.18}; background: ${isDark ? '#a78bfa' : '#7c3aed'}; animation: radioPulse 1.2s ease-in-out infinite alternate; animation-delay: ${i * 0.15}s`" />
        </div>
      </div>
    </div>

    <!-- Now Playing -->
    <NowPlayingCard class="mb-5" />

    <!-- Queue teaser -->
    <div v-if="queueDepth > 0" class="mb-5 bg-yellow-500/10 border border-yellow-500/20 rounded-2xl px-4 py-3 flex items-center gap-3">
      <span class="text-2xl">🎶</span>
      <div>
        <p class="text-yellow-600 dark:text-yellow-300 font-semibold text-sm">{{ queueDepth }} song{{ queueDepth !== 1 ? 's' : '' }} in the queue</p>
        <p class="text-yellow-600/70 text-xs">Jump in — bump your song to the top!</p>
      </div>
    </div>

    <!-- Closed notice -->
    <div v-if="!isOpen" class="mb-5 bg-orange-500/10 border border-orange-500/20 rounded-2xl p-4 text-center">
      <div class="text-3xl mb-1">🌙</div>
      <p class="text-orange-500 font-semibold">Show requests are closed</p>
    </div>

    <!-- CTAs -->
    <div class="flex gap-3 mb-6">
      <button
        v-if="isOpen"
        @click="showModal = true"
        class="flex-1 relative overflow-hidden rounded-2xl transition active:scale-95"
        :style="isDark
          ? 'background: linear-gradient(160deg, #052e16 0%, #14532d 100%); border: 1px solid rgba(34,197,94,0.35); box-shadow: 0 4px 24px rgba(34,197,94,0.2)'
          : 'background: linear-gradient(160deg, #f0fdf4 0%, #dcfce7 100%); border: 1px solid rgba(34,197,94,0.4); box-shadow: 0 2px 12px rgba(34,197,94,0.15)'"
      >
        <div class="absolute -top-4 -right-4 w-20 h-20 rounded-full pointer-events-none"
          style="background: radial-gradient(circle, rgba(34,197,94,0.2) 0%, transparent 70%)" />
        <div class="relative px-3 py-4 flex flex-col items-center gap-1">
          <span class="text-2xl leading-none">🎵</span>
          <span class="font-black text-sm tracking-wide" :class="isDark ? 'text-white' : 'text-green-900'">Request a Song</span>
          <span class="text-xs font-bold rounded-full px-2.5 py-0.5"
            :style="isDark
              ? 'background: rgba(34,197,94,0.15); border: 1px solid rgba(34,197,94,0.3); color: #86efac'
              : 'background: rgba(34,197,94,0.2); border: 1px solid rgba(34,197,94,0.4); color: #15803d'">
            ${{ showStore.config.songRequestCost }}
          </span>
        </div>
      </button>

      <button
        @click="showDonate = true"
        class="flex-1 relative overflow-hidden rounded-2xl transition active:scale-95"
        :style="isDark
          ? 'background: linear-gradient(160deg, #2d0a1e 0%, #500724 100%); border: 1px solid rgba(236,72,153,0.35); box-shadow: 0 4px 24px rgba(236,72,153,0.2)'
          : 'background: linear-gradient(160deg, #fdf2f8 0%, #fce7f3 100%); border: 1px solid rgba(236,72,153,0.35); box-shadow: 0 2px 12px rgba(236,72,153,0.12)'"
      >
        <div class="absolute -top-4 -right-4 w-20 h-20 rounded-full pointer-events-none"
          style="background: radial-gradient(circle, rgba(236,72,153,0.2) 0%, transparent 70%)" />
        <div class="relative px-3 py-4 flex flex-col items-center gap-1">
          <span class="text-2xl leading-none">❤️</span>
          <span class="font-black text-sm tracking-wide" :class="isDark ? 'text-white' : 'text-pink-900'">Donate</span>
          <span class="text-xs font-bold rounded-full px-2.5 py-0.5"
            :style="isDark
              ? 'background: rgba(236,72,153,0.15); border: 1px solid rgba(236,72,153,0.3); color: #f9a8d4'
              : 'background: rgba(236,72,153,0.2); border: 1px solid rgba(236,72,153,0.4); color: #be185d'">
            ${{ showStore.config.donateCost }}+
          </span>
        </div>
      </button>
    </div>

    <!-- Up next preview -->
    <div v-if="showStore.queue.length > 0">
      <p class="text-xs uppercase tracking-widest text-hint mb-3 px-1">Up Next</p>
      <div class="space-y-2">
        <div v-for="(item, i) in showStore.queue.slice(0, 3)" :key="item.id"
          class="rounded-xl border overflow-hidden"
          :class="item.status === 'Playing' ? 'border-green-500/30' : i === 0 ? 'border-yellow-500/20' : 'border-base bg-card'">
          <div class="flex items-center gap-3 px-3 py-2.5"
            :style="item.status === 'Playing'
              ? (isDark ? 'background: rgba(5,46,22,0.5)' : 'background: rgba(220,252,231,0.6)')
              : i === 0 ? (isDark ? 'background: rgba(66,32,6,0.3)' : 'background: rgba(254,249,195,0.6)') : ''">
            <span class="text-lg shrink-0">{{ item.status === 'Playing' ? '▶️' : i === 0 ? '1️⃣' : i === 1 ? '2️⃣' : '3️⃣' }}</span>
            <div class="flex-1 min-w-0">
              <MarqueeText :text="item.title" class="font-medium text-main text-sm" />
              <div class="flex items-center gap-2 mt-0.5">
                <p v-if="item.durationSeconds" class="text-xs text-hint">{{ formatDuration(item.durationSeconds) }}</p>
                <p v-if="item.status === 'Pending' && estimatedWaitSeconds(i) > 0"
                  class="text-xs"
                  :class="i === 0 ? 'text-yellow-500 font-medium' : 'text-hint'">
                  ⏱ {{ formatWait(estimatedWaitSeconds(i)) }}
                </p>
              </div>
            </div>
            <span v-if="item.status === 'Playing'" class="text-xs text-green-500 font-semibold animate-pulse shrink-0">LIVE</span>
            <button
              v-if="item.status === 'Pending' && i !== showStore.queue.findIndex(q => q.status === 'Pending')"
              @click="boost(item)"
              :disabled="bumpingId === item.id"
              class="shrink-0 text-xs font-semibold rounded-lg px-2.5 py-1.5 transition active:scale-95 disabled:opacity-50"
              style="background: linear-gradient(135deg, rgba(234,179,8,0.2), rgba(234,179,8,0.1)); color: #d97706; border: 1px solid rgba(234,179,8,0.3)"
            >
              {{ bumpingId === item.id ? '⏳' : `🚀 $${showStore.config.bumpCost}` }}
            </button>
          </div>
          <div v-if="item.status === 'Pending' && i === 0"
            class="px-3 py-1 bg-yellow-500/10 border-t border-yellow-500/20">
            <span class="text-yellow-500 text-xs font-bold animate-pulse">⚡ YOU'RE UP NEXT!</span>
          </div>
        </div>
      </div>
    </div>

  </div>

  <SongRequestModal v-if="showModal" @close="showModal = false" />
  <DonateModal v-if="showDonate" @close="showDonate = false" />
</template>

<style scoped>
@keyframes radioPulse {
  from { opacity: 0.3; transform: scaleY(0.7); }
  to   { opacity: 1;   transform: scaleY(1.1); }
}
</style>
