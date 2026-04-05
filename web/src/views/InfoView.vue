<script setup>
import { computed } from 'vue'
import { useShowStore } from '@/stores/show'
import { version } from '../../package.json'

const showStore = useShowStore()
const config = computed(() => showStore.config)
const isDark = computed(() => config.value.theme !== 'light')

const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']
const todayName = DAYS[new Date().getDay()]

const schedule = computed(() => {
  if (!config.value.showSchedule?.length) return []
  return DAYS.map(day => {
    const entry = config.value.showSchedule.find(e => e.day === day)
    return entry ?? { day, start: '', end: '', enabled: false }
  })
})

function fmt12(time) {
  if (!time) return ''
  const labels = { dusk: 'Dusk', sunset: 'Sunset', sunrise: 'Sunrise', dawn: 'Dawn' }
  const lower = time.toLowerCase()
  if (labels[lower]) return labels[lower]
  const [h, m] = time.split(':').map(Number)
  if (isNaN(h)) return time
  const ampm = h >= 12 ? 'PM' : 'AM'
  const h12 = h % 12 || 12
  return m === 0 ? `${h12} ${ampm}` : `${h12}:${String(m).padStart(2, '0')} ${ampm}`
}

const radioStyle = computed(() => isDark.value
  ? 'background: linear-gradient(135deg, #0f172a 0%, #1a1040 50%, #0f172a 100%); border: 1px solid rgba(139,92,246,0.35); box-shadow: 0 0 32px rgba(139,92,246,0.15)'
  : 'background: linear-gradient(135deg, #f5f3ff 0%, #ede9fe 50%, #f5f3ff 100%); border: 1px solid rgba(139,92,246,0.3); box-shadow: 0 2px 16px rgba(139,92,246,0.1)')
</script>

<template>
  <div class="max-w-lg md:max-w-2xl mx-auto px-4 md:px-8 pt-4 pb-6">

    <div class="mb-6">
      <h1 class="text-2xl font-black text-main">Show Info</h1>
      <p class="text-hint text-sm mt-1">{{ config.siteName }}</p>
    </div>

    <!-- FM Radio -->
    <div v-if="config.fmRadioStation"
      class="relative mb-5 rounded-2xl overflow-hidden"
      :style="radioStyle">
      <div class="absolute inset-0 pointer-events-none"
        :style="isDark
          ? 'background: radial-gradient(ellipse at 50% 0%, rgba(139,92,246,0.2) 0%, transparent 70%)'
          : 'background: radial-gradient(ellipse at 50% 0%, rgba(139,92,246,0.1) 0%, transparent 70%)'" />
      <div class="relative flex items-center gap-4 px-5 py-4">
        <div class="shrink-0 w-12 h-12 rounded-xl flex items-center justify-center text-2xl"
          :style="isDark
            ? 'background: rgba(139,92,246,0.2); border: 1px solid rgba(139,92,246,0.3)'
            : 'background: rgba(139,92,246,0.15); border: 1px solid rgba(139,92,246,0.25)'">📻</div>
        <div>
          <p class="text-xs font-semibold uppercase tracking-widest mb-0.5"
            :style="isDark ? 'color: rgba(167,139,250,0.8)' : 'color: rgba(109,40,217,0.8)'">
            Tune your radio to
          </p>
          <p class="text-2xl font-black leading-none"
            :style="isDark ? 'color: #ffffff' : 'color: #3b0764'">
            {{ config.fmRadioStation }}
          </p>
          <p class="text-xs mt-1"
            :style="isDark ? 'color: rgba(167,139,250,0.6)' : 'color: rgba(109,40,217,0.6)'">
            to hear the show live
          </p>
        </div>
      </div>
    </div>

    <!-- Show Schedule -->
    <div class="bg-card rounded-2xl border border-base overflow-hidden mb-5">
      <div class="px-5 py-4 border-b border-base flex items-center gap-2">
        <span class="text-lg">📅</span>
        <h2 class="font-bold text-main">Show Schedule</h2>
      </div>

      <div v-if="!schedule.length" class="px-5 py-6 text-center text-hint text-sm">
        Schedule not yet configured — check back soon!
      </div>

      <div v-else>
        <div
          v-for="entry in schedule"
          :key="entry.day"
          class="flex items-center gap-4 px-5 py-3.5 border-b border-base last:border-0 transition"
          :class="entry.day === todayName && entry.enabled ? (isDark ? 'bg-green-500/5' : 'bg-green-50') : ''"
        >
          <div class="w-1.5 h-8 rounded-full shrink-0"
            :class="entry.day === todayName
              ? (entry.enabled ? 'bg-green-500' : 'bg-ghost')
              : 'bg-transparent'" />

          <div class="w-24 shrink-0">
            <span class="font-semibold text-sm"
              :class="entry.day === todayName ? 'text-main' : 'text-sub'">
              {{ entry.day }}
            </span>
            <span v-if="entry.day === todayName"
              class="ml-2 text-[10px] font-bold uppercase tracking-wide text-green-500">Today</span>
          </div>

          <div class="flex-1">
            <span v-if="entry.enabled && entry.start && entry.end"
              class="text-sm font-semibold"
              :class="entry.day === todayName ? 'text-green-600' : 'text-main'">
              {{ fmt12(entry.start) }} – {{ fmt12(entry.end) }}
            </span>
            <span v-else class="text-sm text-hint italic">Closed</span>
          </div>

          <div v-if="entry.day === todayName && entry.enabled" class="shrink-0">
            <span v-if="config.isOpen"
              class="text-xs font-bold text-green-500 bg-green-500/10 border border-green-500/30 rounded-full px-2 py-0.5 animate-pulse">
              LIVE NOW
            </span>
            <span v-else class="text-xs text-hint bg-lift rounded-full px-2 py-0.5">Tonight</span>
          </div>
        </div>
      </div>
    </div>

    <!-- How it works -->
    <div class="bg-card rounded-2xl border border-base overflow-hidden mb-5">
      <div class="px-5 py-4 border-b border-base flex items-center gap-2">
        <span class="text-lg">💡</span>
        <h2 class="font-bold text-main">How It Works</h2>
      </div>
      <div class="px-5 py-4 space-y-4">
        <div class="flex gap-3">
          <div class="w-8 h-8 rounded-full bg-green-500/15 border border-green-500/30 flex items-center justify-center text-sm font-bold text-green-600 shrink-0">1</div>
          <div>
            <p class="text-sm font-semibold text-main">Pick a song</p>
            <p class="text-xs text-hint mt-0.5">Browse the playlist and pick your favorite holiday tune.</p>
          </div>
        </div>
        <div class="flex gap-3">
          <div class="w-8 h-8 rounded-full bg-green-500/15 border border-green-500/30 flex items-center justify-center text-sm font-bold text-green-600 shrink-0">2</div>
          <div>
            <p class="text-sm font-semibold text-main">Make a small donation</p>
            <p class="text-xs text-hint mt-0.5">Just ${{ config.songRequestCost }} adds your song to the queue. 100% goes toward the show.</p>
          </div>
        </div>
        <div class="flex gap-3">
          <div class="w-8 h-8 rounded-full bg-green-500/15 border border-green-500/30 flex items-center justify-center text-sm font-bold text-green-600 shrink-0">3</div>
          <div>
            <p class="text-sm font-semibold text-main">Watch the lights dance</p>
            <p class="text-xs text-hint mt-0.5">Your song plays on the display synced to the music. Rate it when it's done!</p>
          </div>
        </div>
        <div class="flex gap-3">
          <div class="w-8 h-8 rounded-full bg-yellow-500/15 border border-yellow-500/30 flex items-center justify-center text-sm font-bold text-yellow-600 shrink-0">⚡</div>
          <div>
            <p class="text-sm font-semibold text-main">Want to go sooner?</p>
            <p class="text-xs text-hint mt-0.5">Use the Boost button for ${{ config.bumpCost }} to move your song up one spot in the queue.</p>
          </div>
        </div>
      </div>
    </div>

    <!-- Footer -->
    <div class="mt-6 pt-4 border-t border-base text-center space-y-1">
      <p class="text-xs text-hint">
        Created by
        <a href="https://github.com/thebradleysanders/XlightsQueue" target="_blank" rel="noopener"
          class="font-semibold text-sub hover:text-main transition">
          Brad Sanders
        </a>
        &nbsp;·&nbsp;
        <a href="https://github.com/thebradleysanders/XlightsQueue" target="_blank" rel="noopener"
          class="hover:text-main transition inline-flex items-center gap-1">
          <i class="fa-brands fa-github text-sm"></i>
          QueueTheMagic
        </a>
      </p>
      <p class="text-xs text-ghost">v{{ version }}</p>
    </div>

  </div>
</template>
