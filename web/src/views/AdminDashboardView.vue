<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAdminStore } from '@/stores/admin'
import { useShowStore } from '@/stores/show'
import { Line, Bar } from 'vue-chartjs'
import {
  Chart as ChartJS, CategoryScale, LinearScale, PointElement,
  LineElement, BarElement, Title, Tooltip, Legend, Filler
} from 'chart.js'

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, BarElement, Title, Tooltip, Legend, Filler)

const router = useRouter()
const admin = useAdminStore()
const showStore = useShowStore()
const activeTab = ref('reports')
const saving = ref(false)
const saveMsg = ref(null)
const syncSongsMsg = ref(null)
const syncPlaylistsMsg = ref(null)
const scheduleMsg = ref(null)
const mqttMsg = ref(null)
const editConfig = ref(null)
const diagInterval = ref(null)
const playlistEdits = ref({}) // id → {name, isEnabled}
const playlistSaving = ref({})

onMounted(async () => {
  await Promise.all([admin.fetchReports(), admin.fetchRatings(), admin.fetchConfig(), admin.fetchDiagnostics(), admin.fetchPlaylists()])
  editConfig.value = JSON.parse(JSON.stringify(admin.config))
  diagInterval.value = setInterval(() => admin.fetchDiagnostics(), 5000)
})

function starsDisplay(avg) {
  const full = Math.floor(avg)
  const half = avg - full >= 0.5
  return { full, half, empty: 5 - full - (half ? 1 : 0) }
}

function beforeUnmount() {
  clearInterval(diagInterval.value)
}

function logout() {
  admin.logout()
  router.push('/admin/login')
}

async function saveConfig() {
  saving.value = true
  saveMsg.value = null
  try {
    await admin.saveConfig(editConfig.value)
    saveMsg.value = 'Saved!'
    setTimeout(() => saveMsg.value = null, 2000)
  } catch {
    saveMsg.value = 'Save failed.'
  } finally {
    saving.value = false
  }
}

async function syncSongs() {
  syncSongsMsg.value = 'Syncing...'
  try {
    const result = await admin.syncSongs()
    syncSongsMsg.value = `✓ ${result.synced} new songs synced (${result.total} total sequences found)`
  } catch {
    syncSongsMsg.value = 'Sync failed — check FPP address.'
  }
  setTimeout(() => syncSongsMsg.value = null, 5000)
}

async function syncPlaylists() {
  syncPlaylistsMsg.value = 'Syncing...'
  try {
    const result = await admin.syncPlaylists()
    await admin.fetchPlaylists()
    syncPlaylistsMsg.value = `✓ ${result.newPlaylists} new playlists, ${result.newSongs} new songs imported`
  } catch {
    syncPlaylistsMsg.value = 'Sync failed — check FPP address.'
  }
  setTimeout(() => syncPlaylistsMsg.value = null, 5000)
}

function playlistEdit(p) {
  if (!playlistEdits.value[p.id]) playlistEdits.value[p.id] = { name: p.name, isEnabled: p.isEnabled }
  return playlistEdits.value[p.id]
}

async function savePlaylist(p) {
  playlistSaving.value[p.id] = true
  const edit = playlistEdits.value[p.id] ?? { name: p.name, isEnabled: p.isEnabled }
  await admin.updatePlaylist(p.id, edit.name, edit.isEnabled)
  delete playlistEdits.value[p.id]
  playlistSaving.value[p.id] = false
}

async function testMqtt() {
  try {
    await admin.testMqtt()
    mqttMsg.value = 'Test message sent!'
  } catch {
    mqttMsg.value = 'MQTT test failed — check broker settings.'
  }
  setTimeout(() => mqttMsg.value = null, 3000)
}

async function skipItem(id) {
  await admin.skipQueueItem(id)
  await showStore.fetchQueue()
}

// Chart data
const donationChartData = computed(() => ({
  labels: admin.reports?.dailyStats.map(d => d.date.slice(5)) ?? [],
  datasets: [{
    label: 'Donations ($)',
    data: admin.reports?.dailyStats.map(d => Number(d.donations)) ?? [],
    borderColor: '#22c55e',
    backgroundColor: 'rgba(34,197,94,0.1)',
    fill: true,
    tension: 0.3,
  }]
}))

const queueChartData = computed(() => ({
  labels: admin.reports?.dailyStats.map(d => d.date.slice(5)) ?? [],
  datasets: [{
    label: 'Songs Queued',
    data: admin.reports?.dailyStats.map(d => d.songsQueued) ?? [],
    backgroundColor: 'rgba(99,102,241,0.7)',
    borderRadius: 4,
  }]
}))

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: { legend: { display: false } },
  scales: {
    x: { ticks: { color: '#6b7280', maxTicksLimit: 10 }, grid: { color: '#1f2937' } },
    y: { ticks: { color: '#6b7280' }, grid: { color: '#1f2937' } }
  }
}

async function importFppSchedule() {
  scheduleMsg.value = 'Importing...'
  try {
    const result = await admin.syncSchedule()
    if (result.synced) {
      scheduleMsg.value = `Imported — ${result.daysEnabled} days enabled from ${result.totalEntries} FPP entries.`
      // Reload config so the editor reflects the new schedule
      await admin.fetchConfig()
      editConfig.value = JSON.parse(JSON.stringify(admin.config))
    } else {
      scheduleMsg.value = result.message
    }
  } catch {
    scheduleMsg.value = 'Import failed — check FPP connection.'
  }
  setTimeout(() => scheduleMsg.value = null, 4000)
}

// Schedule helpers
const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

const scheduleEntries = computed({
  get: () => {
    let entries = []
    try { entries = JSON.parse(editConfig.value?.showScheduleJson || '[]') } catch { }
    // Ensure all 7 days are present
    return DAYS.map(day => entries.find(e => e.day === day) ?? { day, start: '17:00', end: '22:00', enabled: false })
  },
  set: (val) => { editConfig.value.showScheduleJson = JSON.stringify(val) }
})

function updateScheduleEntry(day, field, value) {
  const entries = scheduleEntries.value.map(e => e.day === day ? { ...e, [field]: value } : e)
  scheduleEntries.value = entries
}

// Social media helpers
function addSocialLink() {
  const links = JSON.parse(editConfig.value.socialMediaJson || '[]')
  links.push({ platform: '', url: '' })
  editConfig.value.socialMediaJson = JSON.stringify(links)
}
function removeSocialLink(i) {
  const links = JSON.parse(editConfig.value.socialMediaJson || '[]')
  links.splice(i, 1)
  editConfig.value.socialMediaJson = JSON.stringify(links)
}
const socialLinks = computed({
  get: () => {
    try { return JSON.parse(editConfig.value?.socialMediaJson || '[]') } catch { return [] }
  },
  set: (val) => { editConfig.value.socialMediaJson = JSON.stringify(val) }
})
</script>

<template>
  <main class="max-w-5xl mx-auto px-4 py-8">
    <div class="flex items-center justify-between mb-8">
      <h1 class="text-2xl font-bold">Admin Dashboard</h1>
      <button @click="logout" class="text-sm text-gray-500 hover:text-white">Logout</button>
    </div>

    <!-- Tabs -->
    <div class="flex gap-1 bg-gray-900 rounded-xl p-1 mb-8 border border-gray-800 flex-wrap">
      <button v-for="tab in ['reports', 'playlists', 'config', 'queue']" :key="tab"
        @click="activeTab = tab"
        class="flex-1 py-2 text-sm rounded-lg capitalize transition min-w-0"
        :class="activeTab === tab ? 'bg-gray-700 text-white font-medium' : 'text-gray-400 hover:text-white'">
        {{ tab === 'reports' ? '📊 Reports' : tab === 'playlists' ? '🎶 Playlists' : tab === 'config' ? '⚙️ Config' : '🎵 Queue' }}
      </button>
    </div>

    <!-- Reports Tab (includes Ratings) -->
    <div v-if="activeTab === 'reports'">

      <!-- Donation + queue stat cards -->
      <div v-if="admin.reports" class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-gray-900 rounded-xl p-4 border border-gray-800">
          <p class="text-xs text-gray-500 mb-1">Total Donations</p>
          <p class="text-2xl font-bold text-green-400">${{ Number(admin.reports.totalDonations).toFixed(2) }}</p>
        </div>
        <div class="bg-gray-900 rounded-xl p-4 border border-gray-800">
          <p class="text-xs text-gray-500 mb-1">Songs Played</p>
          <p class="text-2xl font-bold text-white">{{ admin.reports.totalSongsPlayed }}</p>
        </div>
        <div class="bg-gray-900 rounded-xl p-4 border border-gray-800">
          <p class="text-xs text-gray-500 mb-1">Highest Donation</p>
          <p class="text-2xl font-bold text-yellow-400">${{ Number(admin.reports.highestDonation).toFixed(2) }}</p>
        </div>
        <div class="bg-gray-900 rounded-xl p-4 border border-gray-800">
          <p class="text-xs text-gray-500 mb-1">Top Song</p>
          <p class="text-sm font-bold text-white truncate">{{ admin.reports.topSong || '—' }}</p>
        </div>
      </div>

      <!-- Charts -->
      <div v-if="admin.reports" class="grid md:grid-cols-2 gap-6 mb-8">
        <div class="bg-gray-900 rounded-xl p-5 border border-gray-800">
          <p class="text-sm text-gray-400 mb-4">Daily Donations (30d)</p>
          <div class="h-48">
            <Line :data="donationChartData" :options="chartOptions" />
          </div>
        </div>
        <div class="bg-gray-900 rounded-xl p-5 border border-gray-800">
          <p class="text-sm text-gray-400 mb-4">Songs Queued (30d)</p>
          <div class="h-48">
            <Bar :data="queueChartData" :options="chartOptions" />
          </div>
        </div>
      </div>

      <!-- Song Ratings section -->
      <div class="flex items-center justify-between mb-4">
        <h2 class="text-sm font-semibold text-gray-300 uppercase tracking-wide">⭐ Song Ratings</h2>
        <button @click="admin.fetchRatings()" class="text-xs px-3 py-1.5 bg-gray-800 hover:bg-gray-700 text-gray-300 rounded-lg">
          Refresh
        </button>
      </div>

      <div v-if="admin.ratings.length === 0" class="bg-gray-900 rounded-xl border border-gray-800 py-10 text-center text-gray-600 text-sm">
        No ratings yet — guests can rate songs while they play.
      </div>

      <div v-else>
        <!-- Rating stat cards -->
        <div class="grid grid-cols-3 gap-4 mb-4">
          <div class="bg-gray-900 rounded-xl p-4 border border-gray-800 text-center">
            <p class="text-xs text-gray-500 mb-1">Songs Rated</p>
            <p class="text-2xl font-bold text-white">{{ admin.ratings.length }}</p>
          </div>
          <div class="bg-gray-900 rounded-xl p-4 border border-gray-800 text-center">
            <p class="text-xs text-gray-500 mb-1">Total Votes</p>
            <p class="text-2xl font-bold text-yellow-400">{{ admin.ratings.reduce((s, r) => s + r.totalRatings, 0) }}</p>
          </div>
          <div class="bg-gray-900 rounded-xl p-4 border border-gray-800 text-center">
            <p class="text-xs text-gray-500 mb-1">Overall Avg</p>
            <p class="text-2xl font-bold text-green-400">
              {{ (admin.ratings.reduce((s, r) => s + r.averageRating * r.totalRatings, 0) / admin.ratings.reduce((s, r) => s + r.totalRatings, 0)).toFixed(1) }}
            </p>
          </div>
        </div>

        <!-- Ratings table -->
        <div class="bg-gray-900 rounded-xl border border-gray-800 overflow-hidden">
          <div class="grid grid-cols-12 text-xs text-gray-500 uppercase tracking-wide px-4 py-2.5 border-b border-gray-800 bg-gray-950">
            <span class="col-span-5">Song</span>
            <span class="col-span-3">Rating</span>
            <span class="col-span-2 text-center">Votes</span>
            <span class="col-span-2 text-right">Breakdown</span>
          </div>
          <div v-for="(r, i) in admin.ratings" :key="r.songId"
            class="grid grid-cols-12 items-center px-4 py-3 border-b border-gray-800/50 last:border-0"
            :class="i % 2 === 0 ? '' : 'bg-gray-900/50'">
            <div class="col-span-5 min-w-0 pr-2">
              <p class="text-sm font-medium text-white truncate">{{ r.title }}</p>
              <p class="text-xs text-gray-500 truncate">{{ r.artist || r.playlistName }}</p>
            </div>
            <div class="col-span-3 flex items-center gap-1.5">
              <span class="text-yellow-400 text-base leading-none">
                <span v-for="n in starsDisplay(r.averageRating).full" :key="'f'+n">★</span>
                <span v-if="starsDisplay(r.averageRating).half">½</span>
                <span v-for="n in starsDisplay(r.averageRating).empty" :key="'e'+n" class="opacity-25">★</span>
              </span>
              <span class="text-sm font-bold text-white">{{ r.averageRating }}</span>
            </div>
            <div class="col-span-2 text-center">
              <span class="text-sm text-gray-400">{{ r.totalRatings }}</span>
            </div>
            <div class="col-span-2 flex flex-col gap-0.5 items-end">
              <div v-for="(count, si) in [r.fiveStars, r.fourStars, r.threeStars, r.twoStars, r.oneStar]" :key="si"
                class="flex items-center gap-1 w-full justify-end">
                <div class="h-1.5 rounded-full bg-yellow-500/60" :style="`width: ${r.totalRatings > 0 ? (count / r.totalRatings * 48) : 0}px`" />
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Playlists Tab -->
    <div v-if="activeTab === 'playlists'">
      <div v-if="admin.playlists.length === 0"
        class="bg-gray-900 rounded-xl border border-gray-800 py-10 text-center">
        <p class="text-gray-400 font-medium mb-1">No playlists imported yet</p>
        <p class="text-gray-600 text-sm">Go to Config → Falcon Player and click <strong class="text-gray-400">Import Playlists</strong>.</p>
      </div>

      <div class="space-y-3">
        <div v-for="p in admin.playlists" :key="p.id"
          class="bg-gray-900 rounded-xl p-4 border border-gray-800 flex flex-col md:flex-row md:items-center gap-3">
          <div class="flex-1 min-w-0">
            <input
              :value="(playlistEdits[p.id] ?? p).name"
              @input="e => { playlistEdit(p).name = e.target.value }"
              class="input w-full"
            />
            <p class="text-xs text-gray-600 mt-1 truncate">FPP: {{ p.fppPlaylistName }} · {{ p.songCount }} songs</p>
          </div>
          <div class="flex items-center gap-4 shrink-0">
            <label class="flex items-center gap-2 text-sm text-gray-300 cursor-pointer">
              <input type="checkbox"
                :checked="(playlistEdits[p.id] ?? p).isEnabled"
                @change="e => { playlistEdit(p).isEnabled = e.target.checked }"
                class="accent-green-500" />
              Visible to users
            </label>
            <button @click="savePlaylist(p)"
              :disabled="playlistSaving[p.id]"
              class="text-sm px-3 py-1.5 bg-green-600 hover:bg-green-500 disabled:opacity-40 text-white rounded-lg">
              {{ playlistSaving[p.id] ? '...' : 'Save' }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Config Tab -->
    <div v-if="activeTab === 'config' && editConfig">
      <form @submit.prevent="saveConfig" class="space-y-6">
        <!-- Season toggle -->
        <section class="rounded-xl p-5 border"
          :class="editConfig.isSeasonActive ? 'bg-green-500/5 border-green-500/30' : 'bg-red-500/5 border-red-500/30'">
          <div class="flex items-center justify-between gap-4">
            <div>
              <h3 class="text-sm font-semibold uppercase tracking-wide"
                :class="editConfig.isSeasonActive ? 'text-green-300' : 'text-red-300'">
                {{ editConfig.isSeasonActive ? '🟢 Season Active' : '🔴 Season Inactive' }}
              </h3>
              <p class="text-xs text-gray-500 mt-1">
                {{ editConfig.isSeasonActive ? 'The public site is live. Visitors can request songs.' : 'The public site shows an off-season message. No requests accepted.' }}
              </p>
            </div>
            <button type="button" @click="editConfig.isSeasonActive = !editConfig.isSeasonActive"
              class="shrink-0 px-5 py-2.5 rounded-xl text-sm font-bold transition"
              :class="editConfig.isSeasonActive
                ? 'bg-red-500/15 text-red-300 border border-red-500/30 hover:bg-red-500/25'
                : 'bg-green-500/15 text-green-300 border border-green-500/30 hover:bg-green-500/25'">
              {{ editConfig.isSeasonActive ? 'Turn Off' : 'Turn On' }}
            </button>
          </div>
          <div v-if="!editConfig.isSeasonActive" class="mt-4">
            <label class="block">
              <span class="text-xs text-gray-400">Off-season message shown to visitors</span>
              <textarea v-model="editConfig.offSeasonMessage" rows="2"
                class="input mt-1 resize-none"
                placeholder="The holiday light show is not running right now. Check back soon!" />
            </label>
          </div>
        </section>

        <!-- FPP -->
        <section class="bg-gray-900 rounded-xl p-5 border border-gray-800">
          <h3 class="text-sm font-semibold text-gray-300 mb-4 uppercase tracking-wide">Falcon Player (FPP)</h3>

          <!-- Connection -->
          <div class="grid md:grid-cols-2 gap-4">
            <label class="block">
              <span class="text-xs text-gray-400">FPP Address</span>
              <input v-model="editConfig.fppAddress" class="input mt-1" placeholder="192.168.1.100" />
            </label>
            <label class="block">
              <span class="text-xs text-gray-400">Default Playlist Name</span>
              <select v-if="admin.playlists.length" v-model="editConfig.defaultPlaylistName" class="input mt-1">
                <option value="">— None —</option>
                <option v-for="p in admin.playlists" :key="p.id" :value="p.fppPlaylistName">
                  {{ p.fppPlaylistName }}
                </option>
              </select>
              <input v-else v-model="editConfig.defaultPlaylistName" class="input mt-1" placeholder="Import playlists first" />
            </label>
            <label class="block">
              <span class="text-xs text-gray-400">Polling Interval (seconds)</span>
              <input type="number" min="2" v-model="editConfig.fppPollingIntervalSeconds" class="input mt-1" />
            </label>
          </div>
          <div class="flex gap-6 mt-4">
            <label class="flex items-center gap-2 text-sm text-gray-300 cursor-pointer">
              <input type="checkbox" v-model="editConfig.autoPlayDefault" class="accent-green-500" />
              Auto-play default playlist
            </label>
            <label class="flex items-center gap-2 text-sm text-gray-300 cursor-pointer">
              <input type="checkbox" v-model="editConfig.interruptForUserSongs" class="accent-green-500" />
              Interrupt for user songs
            </label>
          </div>

          <!-- Import section -->
          <div class="mt-5 pt-4 border-t border-gray-800">
            <p class="text-xs text-gray-500 mb-3 uppercase tracking-wide font-semibold">Import from FPP</p>
            <p class="text-xs text-gray-600 mb-4">Save your FPP address above first, then import your playlists and songs. Re-run after adding new sequences in FPP.</p>
            <div class="flex flex-wrap gap-3">
              <div class="flex flex-col gap-1">
                <button type="button" @click="syncPlaylists"
                  class="text-sm px-4 py-2.5 rounded-lg text-white font-medium transition"
                  style="background: linear-gradient(135deg, #312e81, #4338ca); border: 1px solid rgba(99,102,241,0.4)">
                  🎶 Import Playlists &amp; Songs
                </button>
                <p v-if="syncPlaylistsMsg" class="text-xs px-1"
                  :class="syncPlaylistsMsg.startsWith('✓') ? 'text-green-400' : syncPlaylistsMsg === 'Syncing...' ? 'text-gray-400' : 'text-red-400'">
                  {{ syncPlaylistsMsg }}
                </p>
              </div>
              <div class="flex flex-col gap-1">
                <button type="button" @click="syncSongs"
                  class="text-sm px-4 py-2.5 rounded-lg text-white font-medium transition"
                  style="background: linear-gradient(135deg, #1e3a5f, #1e40af); border: 1px solid rgba(59,130,246,0.4)">
                  🎵 Import Sequences (songs only)
                </button>
                <p v-if="syncSongsMsg" class="text-xs px-1"
                  :class="syncSongsMsg.startsWith('✓') ? 'text-green-400' : syncSongsMsg === 'Syncing...' ? 'text-gray-400' : 'text-red-400'">
                  {{ syncSongsMsg }}
                </p>
              </div>
            </div>
          </div>
        </section>

        <!-- Site -->
        <section class="bg-gray-900 rounded-xl p-5 border border-gray-800">
          <h3 class="text-sm font-semibold text-gray-300 mb-4 uppercase tracking-wide">Site</h3>
          <div class="grid md:grid-cols-2 gap-4">
            <label class="block">
              <span class="text-xs text-gray-400">Site Name</span>
              <input v-model="editConfig.siteName" class="input mt-1" />
            </label>
            <label class="block">
              <span class="text-xs text-gray-400">FM Radio Station</span>
              <input v-model="editConfig.fmRadioStation" class="input mt-1" placeholder="e.g. 90.5 FM" />
            </label>
            <label class="block">
              <span class="text-xs text-gray-400">Default Theme</span>
              <select v-model="editConfig.defaultTheme" class="input mt-1">
                <option value="dark">Dark</option>
                <option value="light">Light</option>
              </select>
            </label>
          </div>
        </section>

        <!-- Weekly Schedule -->
        <section class="bg-gray-900 rounded-xl p-5 border border-gray-800">
          <div class="flex items-start justify-between gap-4 mb-1">
            <div>
              <h3 class="text-sm font-semibold text-gray-300 uppercase tracking-wide">Weekly Show Schedule</h3>
              <p class="text-xs text-gray-500 mt-1">Displayed on the public Info page. Import directly from your FPP scheduler.</p>
            </div>
            <button type="button" @click="importFppSchedule"
              class="shrink-0 text-sm px-3 py-1.5 rounded-lg text-white transition whitespace-nowrap"
              style="background: linear-gradient(135deg, #1e3a5f, #1e40af); border: 1px solid rgba(59,130,246,0.4)">
              📅 Import from FPP
            </button>
          </div>
          <p v-if="scheduleMsg" class="text-xs text-blue-400 mb-3">{{ scheduleMsg }}</p>
          <div v-else class="mb-4" />
          <div class="space-y-2">
            <div v-for="entry in scheduleEntries" :key="entry.day"
              class="flex items-center gap-3 rounded-lg px-3 py-2.5 border"
              :class="entry.enabled ? 'bg-gray-800 border-gray-700' : 'bg-gray-900 border-gray-800'">
              <!-- Enable toggle -->
              <input type="checkbox"
                :checked="entry.enabled"
                @change="e => updateScheduleEntry(entry.day, 'enabled', e.target.checked)"
                class="accent-green-500 shrink-0" />
              <!-- Day name -->
              <span class="w-24 text-sm shrink-0"
                :class="entry.enabled ? 'text-white font-medium' : 'text-gray-500'">
                {{ entry.day }}
              </span>
              <!-- Times -->
              <div class="flex items-center gap-2 flex-1" :class="entry.enabled ? '' : 'opacity-40 pointer-events-none'">
                <input type="time"
                  :value="entry.start"
                  @change="e => updateScheduleEntry(entry.day, 'start', e.target.value)"
                  class="input flex-1 text-xs py-1.5" />
                <span class="text-gray-500 text-xs shrink-0">to</span>
                <input type="time"
                  :value="entry.end"
                  @change="e => updateScheduleEntry(entry.day, 'end', e.target.value)"
                  class="input flex-1 text-xs py-1.5" />
              </div>
              <span v-if="!entry.enabled" class="text-xs text-gray-600 shrink-0">Closed</span>
            </div>
          </div>
        </section>

        <!-- Pricing -->
        <section class="bg-gray-900 rounded-xl p-5 border border-gray-800">
          <h3 class="text-sm font-semibold text-gray-300 mb-4 uppercase tracking-wide">Pricing</h3>
          <div class="grid md:grid-cols-2 gap-4">
            <label class="block">
              <span class="text-xs text-gray-400">Song Request Cost ($)</span>
              <input type="number" step="0.01" min="0.50" v-model="editConfig.songRequestCost" class="input mt-1" />
            </label>
            <label class="block">
              <span class="text-xs text-gray-400">Bump Cost ($)</span>
              <input type="number" step="0.01" min="0.50" v-model="editConfig.bumpCost" class="input mt-1" />
            </label>
            <label class="block">
              <span class="text-xs text-gray-400">Min "Just Because" Donation ($)</span>
              <input type="number" step="0.01" min="1.00" v-model="editConfig.donateCost" class="input mt-1" />
            </label>
          </div>
        </section>

        <!-- Rate Limits -->
        <section class="bg-gray-900 rounded-xl p-5 border border-gray-800">
          <h3 class="text-sm font-semibold text-gray-300 mb-4 uppercase tracking-wide">Rate Limits</h3>
          <div class="grid md:grid-cols-2 gap-4">
            <label class="block">
              <span class="text-xs text-gray-400">Max Songs Per Window</span>
              <input type="number" min="1" v-model="editConfig.maxSongsPerWindow" class="input mt-1" />
            </label>
            <label class="block">
              <span class="text-xs text-gray-400">Window (minutes)</span>
              <input type="number" min="1" v-model="editConfig.rateLimitWindowMinutes" class="input mt-1" />
            </label>
          </div>
        </section>

        <!-- MQTT -->
        <section class="bg-gray-900 rounded-xl p-5 border border-gray-800">
          <h3 class="text-sm font-semibold text-gray-300 mb-4 uppercase tracking-wide">MQTT</h3>
          <label class="flex items-center gap-2 text-sm text-gray-300 cursor-pointer mb-4">
            <input type="checkbox" v-model="editConfig.mqttEnabled" class="accent-green-500" />
            Enable MQTT
          </label>
          <div v-if="editConfig.mqttEnabled" class="grid md:grid-cols-2 gap-4">
            <label class="block">
              <span class="text-xs text-gray-400">Broker Host</span>
              <input v-model="editConfig.mqttBrokerHost" class="input mt-1" placeholder="192.168.1.50" />
            </label>
            <label class="block">
              <span class="text-xs text-gray-400">Broker Port</span>
              <input type="number" v-model="editConfig.mqttBrokerPort" class="input mt-1" />
            </label>
            <label class="block">
              <span class="text-xs text-gray-400">Username (optional)</span>
              <input v-model="editConfig.mqttUsername" class="input mt-1" />
            </label>
            <label class="block">
              <span class="text-xs text-gray-400">Password (optional)</span>
              <input type="password" v-model="editConfig.mqttPassword" class="input mt-1" />
            </label>
            <label class="block">
              <span class="text-xs text-gray-400">Topic Prefix</span>
              <input v-model="editConfig.mqttTopicPrefix" class="input mt-1" placeholder="qtm" />
            </label>
          </div>
          <div v-if="editConfig.mqttEnabled" class="mt-4">
            <button type="button" @click="testMqtt" class="text-sm px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg">
              Send Test Message
            </button>
            <p v-if="mqttMsg" class="text-xs text-green-400 mt-2">{{ mqttMsg }}</p>
          </div>
        </section>

        <!-- Social Media -->
        <section class="bg-gray-900 rounded-xl p-5 border border-gray-800">
          <h3 class="text-sm font-semibold text-gray-300 mb-4 uppercase tracking-wide">Social Media</h3>
          <div v-for="(link, i) in socialLinks" :key="i" class="flex gap-2 mb-2">
            <input v-model="link.platform" class="input flex-1" placeholder="Platform (e.g. facebook)" @input="socialLinks = [...socialLinks]" />
            <input v-model="link.url" class="input flex-2 grow" placeholder="https://..." @input="socialLinks = [...socialLinks]" />
            <button type="button" @click="removeSocialLink(i)" class="text-red-500 hover:text-red-400 px-2">✕</button>
          </div>
          <button type="button" @click="addSocialLink" class="text-sm text-green-400 hover:text-green-300 mt-2">+ Add link</button>
        </section>

        <div class="flex items-center gap-4">
          <button type="submit" :disabled="saving"
            class="px-6 py-2.5 bg-green-600 hover:bg-green-500 disabled:opacity-50 text-white font-semibold rounded-xl">
            {{ saving ? 'Saving...' : 'Save Configuration' }}
          </button>
          <span v-if="saveMsg" class="text-sm text-green-400">{{ saveMsg }}</span>
        </div>
      </form>
    </div>

    <!-- Queue Tab -->
    <div v-if="activeTab === 'queue'">
      <!-- Diagnostics -->
      <div class="bg-gray-900 rounded-xl p-5 border border-gray-800 mb-6" v-if="admin.diagnostics">
        <h3 class="text-sm font-semibold text-gray-300 mb-3 uppercase tracking-wide">State Machine</h3>
        <div class="flex gap-4 mb-4">
          <div>
            <span class="text-xs text-gray-500">State</span>
            <p class="font-mono text-green-400 font-bold">{{ admin.diagnostics.state }}</p>
          </div>
          <div>
            <span class="text-xs text-gray-500">Sync</span>
            <p class="font-mono text-white">{{ admin.diagnostics.syncStatus }}</p>
          </div>
          <div>
            <span class="text-xs text-gray-500">Active Item</span>
            <p class="font-mono text-white">{{ admin.diagnostics.activeQueueItemId ?? '—' }}</p>
          </div>
        </div>

        <div class="mt-4">
          <p class="text-xs text-gray-500 mb-2">FPP Command Log (last 30)</p>
          <div class="bg-gray-950 rounded-lg p-3 font-mono text-xs max-h-48 overflow-y-auto space-y-1">
            <div v-for="(cmd, i) in admin.diagnostics.commandLog.slice().reverse()" :key="i"
              class="flex gap-2 text-gray-400">
              <span class="text-gray-600">{{ cmd.timestamp?.slice(11,19) }}</span>
              <span :class="cmd.statusCode === 200 ? 'text-green-400' : cmd.statusCode === 0 ? 'text-red-400' : 'text-yellow-400'">
                {{ cmd.statusCode || 'ERR' }}
              </span>
              <span class="text-gray-500">{{ cmd.durationMs }}ms</span>
              <span class="truncate">{{ cmd.url }}</span>
            </div>
            <p v-if="!admin.diagnostics.commandLog.length" class="text-gray-600">No commands yet.</p>
          </div>
        </div>
      </div>

      <!-- Live queue -->
      <div class="bg-gray-900 rounded-xl p-5 border border-gray-800">
        <h3 class="text-sm font-semibold text-gray-300 mb-3 uppercase tracking-wide">Live Queue</h3>
        <div v-if="showStore.queue.length === 0" class="text-gray-500 text-sm">Queue is empty.</div>
        <div v-else class="space-y-2">
          <div v-for="item in showStore.queue" :key="item.id"
            class="flex items-center gap-3 bg-gray-950 rounded-lg px-4 py-2.5">
            <span class="text-gray-600 text-sm w-6">{{ item.position }}</span>
            <div class="flex-1 min-w-0">
              <p class="text-sm font-medium text-white">{{ item.title }}</p>
              <p class="text-xs text-gray-500">{{ item.artist }} · {{ item.status }}</p>
            </div>
            <button @click="skipItem(item.id)"
              class="text-xs text-red-400 hover:text-red-300 px-3 py-1 bg-red-500/10 rounded-lg">
              Skip
            </button>
          </div>
        </div>
      </div>
    </div>
  </main>
</template>

<style scoped>
@reference "tailwindcss";
.input {
  @apply w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-green-500;
}
</style>
