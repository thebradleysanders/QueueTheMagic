<script setup>
import { ref, computed } from 'vue'
import MarqueeText from '@/components/MarqueeText.vue'
import { useShowStore } from '@/stores/show'
import { useSessionStore } from '@/stores/session'
import { usePayment } from '@/composables/usePayment'
import axios from 'axios'

const showStore = useShowStore()
const sessionStore = useSessionStore()
const { createIntent, confirmPayment } = usePayment()
const bumpingId = ref(null)
const isDev = import.meta.env.DEV
const isDark = computed(() => showStore.config.theme !== 'light')

async function bump(item) {
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

const firstPendingIndex = computed(() => showStore.queue.findIndex(q => q.status === 'Pending'))

const positionEmoji = ['🥇', '🥈', '🥉', '4️⃣', '5️⃣', '6️⃣', '7️⃣', '8️⃣', '9️⃣']

function rowStyle(item, i) {
  if (item.status === 'Playing') {
    return isDark.value
      ? 'background: linear-gradient(to right, #052e16, #111827)'
      : 'background: linear-gradient(to right, #dcfce7, #ffffff)'
  }
  if (i === 0) {
    return isDark.value
      ? 'background: linear-gradient(to right, rgba(66,32,6,0.6), #111827)'
      : 'background: linear-gradient(to right, #fef9c3, #ffffff)'
  }
  return ''
}
</script>

<template>
  <div>
    <!-- Empty state -->
    <div v-if="showStore.queue.length === 0" class="text-center py-12 px-4">
      <div class="text-5xl mb-3">✨</div>
      <p class="text-main font-bold text-lg">Queue is empty!</p>
      <p class="text-hint text-sm mt-1">Be the first to light up the night 🎄</p>
    </div>

    <!-- Queue items -->
    <div v-else class="space-y-3">
      <div
        v-for="(item, i) in showStore.queue"
        :key="item.id"
        class="rounded-2xl border overflow-hidden transition"
        :class="item.status === 'Playing' ? 'border-green-500/40' : i === 0 ? 'border-yellow-500/30' : 'border-base bg-card'"
        :style="rowStyle(item, i)"
      >
        <div class="flex items-center gap-3 px-4 py-3.5">
          <div class="shrink-0 text-2xl w-8 text-center">
            {{ item.status === 'Playing' ? '▶️' : (positionEmoji[i] ?? `${i + 1}`) }}
          </div>

          <div class="flex-1 min-w-0">
            <MarqueeText :text="item.title" class="font-semibold text-main text-sm leading-snug" />
            <p class="text-xs text-hint truncate mt-0.5">{{ item.artist || item.playlistName }}</p>
            <div class="flex items-center gap-2 mt-1">
              <span v-if="item.durationSeconds" class="text-xs text-hint">
                {{ formatDuration(item.durationSeconds) }}
              </span>
              <span v-if="item.status === 'Pending' && estimatedWaitSeconds(i) > 0"
                class="text-xs font-medium"
                :class="i === 0 ? 'text-yellow-500' : 'text-hint'">
                {{ i === 0 ? '⚡ ' : '⏱ ' }}{{ formatWait(estimatedWaitSeconds(i)) }} away
              </span>
            </div>
          </div>

          <div class="shrink-0 flex flex-col items-end gap-1.5">
            <span v-if="item.status === 'Playing'"
              class="text-xs font-bold text-green-500 bg-green-500/10 rounded-full px-2 py-0.5 animate-pulse">
              LIVE
            </span>
            <button
              v-if="item.status === 'Pending' && i !== firstPendingIndex"
              @click="bump(item)"
              :disabled="bumpingId === item.id"
              class="text-xs font-semibold rounded-xl px-3 py-1.5 transition active:scale-95 disabled:opacity-50"
              style="background: linear-gradient(135deg, rgba(234,179,8,0.2), rgba(234,179,8,0.1)); color: #d97706; border: 1px solid rgba(234,179,8,0.3)"
            >
              {{ bumpingId === item.id ? '⏳' : `🚀 Boost $${showStore.config.bumpCost}` }}
            </button>
          </div>
        </div>

        <div v-if="item.status === 'Pending' && i === 0"
          class="px-4 py-1.5 bg-yellow-500/10 border-t border-yellow-500/20 flex items-center gap-2">
          <span class="text-yellow-500 text-xs font-bold animate-pulse">⚡ YOU'RE UP NEXT — GET READY!</span>
        </div>
      </div>
    </div>
  </div>
</template>
