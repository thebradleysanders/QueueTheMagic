<script setup>
    import { BarElement, CategoryScale, Chart as ChartJS, Filler, Legend, LineElement, LinearScale, PointElement, Title, Tooltip } from 'chart.js';
    import { Bar, Line } from 'vue-chartjs';
    import { useRouter } from 'vue-router';

    import { computed, onMounted, onUnmounted, ref } from 'vue';

    import { useAdminStore } from '@/stores/admin';
    import { useShowStore } from '@/stores/show';

    ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, BarElement, Title, Tooltip, Legend, Filler);

    const router = useRouter();
    const admin = useAdminStore();
    const showStore = useShowStore();
    const activeTab = ref('reports');
    const saving = ref(false);
    const saveMsg = ref(null);
    const syncSongsMsg = ref(null);
    const syncPlaylistsMsg = ref(null);
    const scheduleMsg = ref(null);
    const mqttMsg = ref(null);
    const editConfig = ref(null);
    const diagInterval = ref(null);
    const playlistEdits = ref({}); // id → {name, isEnabled}
    const playlistSaving = ref({});
    const expandedPlaylists = ref({}); // id → bool
    const playlistSongs = ref({}); // id → SongAdminDto[]
    const songEdits = ref({}); // songId → {title, artist}
    const songSaving = ref({}); // songId → bool

    onMounted(async () => {
        await Promise.all([admin.fetchReports(), admin.fetchSupporters(), admin.fetchGuests(), admin.fetchRatings(), admin.fetchConfig(), admin.fetchDiagnostics(), admin.fetchPlaylists()]);
        editConfig.value = JSON.parse(JSON.stringify(admin.config));
        diagInterval.value = setInterval(() => admin.fetchDiagnostics(), 5000);
    });

    function starsDisplay(avg) {
        const full = Math.floor(avg);
        const half = avg - full >= 0.5;
        return { full, half, empty: 5 - full - (half ? 1 : 0) };
    }

    function beforeUnmount() {
        clearInterval(diagInterval.value);
    }

    function logout() {
        admin.logout();
        router.push('/admin/login');
    }

    async function saveConfig() {
        saving.value = true;
        saveMsg.value = null;
        try {
            // Empty number boxes come through as '' but the server wants null
            for (const k of ['latitude', 'longitude']) if (editConfig.value[k] === '') editConfig.value[k] = null;
            await admin.saveConfig(editConfig.value);
            saveMsg.value = 'Saved!';
            setTimeout(() => (saveMsg.value = null), 2000);
        } catch {
            saveMsg.value = 'Save failed.';
        } finally {
            saving.value = false;
        }
    }

    const modeOptions = [
        { value: 'auto', label: 'Automatic' },
        { value: 'on', label: 'Force on' },
        { value: 'off', label: 'Force off' }
    ];
    const modeMsg = ref(null);
    async function chooseMode(mode) {
        try {
            editConfig.value.showMode = await admin.setShowMode(mode);
            modeMsg.value = 'Applied.';
        } catch {
            modeMsg.value = 'Could not change mode.';
        }
        setTimeout(() => (modeMsg.value = null), 2000);
    }
    const seasonLive = computed(() => {
        const c = editConfig.value;
        if (!c) return false;
        if (c.showMode === 'on') return true;
        if (c.showMode === 'off') return false;
        const d = new Date();
        const today = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
        return !(c.seasonStart && today < c.seasonStart) && !(c.seasonEnd && today > c.seasonEnd);
    });
    const modeHelp = computed(() => {
        const m = editConfig.value?.showMode;
        if (m === 'on') return 'Forced on: the show runs now, ignoring the season dates and weekly hours.';
        if (m === 'off') return 'Forced off: the show is closed regardless of dates or hours.';
        return 'Automatic: follows the season dates and weekly hours below.';
    });

    // Friends & family passes
    const newGuestName = ref('');
    const guestMsg = ref(null);
    async function createGuest() {
        const name = newGuestName.value.trim();
        if (!name) return;
        try {
            const g = await admin.addGuest(name);
            newGuestName.value = '';
            guestMsg.value = `${g.name}'s code is ${g.code}`;
        } catch (e) {
            guestMsg.value = e.response?.data?.error || 'Could not add the pass.';
        }
        setTimeout(() => (guestMsg.value = null), 6000);
    }
    async function copyGuest(g, what) {
        const text = what === 'code' ? g.code : `${window.location.origin}/?guest=${g.code}`;
        try {
            await navigator.clipboard.writeText(text);
            guestMsg.value = what === 'code' ? `Copied code ${g.code}` : `Copied sign-in link for ${g.name}`;
        } catch {
            guestMsg.value = text;
        }
        setTimeout(() => (guestMsg.value = null), 4000);
    }
    async function removeGuest(g) {
        if (!window.confirm(`Delete ${g.name}'s pass? They will be signed out and can no longer request songs.`)) return;
        await admin.deleteGuest(g.id);
    }

    const listenStatus = ref(null);
    async function refreshListenStatus() {
        try {
            listenStatus.value = await admin.fetchListenStatus();
        } catch {
            listenStatus.value = null;
        }
    }
    refreshListenStatus();
    const listenTimer = setInterval(refreshListenStatus, 10000);
    onUnmounted(() => clearInterval(listenTimer));

    async function goHome() {
        router.push('/');
    }

    async function syncSongs() {
        syncSongsMsg.value = 'Syncing...';
        try {
            const result = await admin.syncSongs();
            syncSongsMsg.value = `✓ ${result.synced} new songs synced (${result.total} total sequences found)`;
        } catch {
            syncSongsMsg.value = 'Sync failed. Check the FPP address.';
        }
        setTimeout(() => (syncSongsMsg.value = null), 5000);
    }

    async function syncPlaylists() {
        syncPlaylistsMsg.value = 'Syncing...';
        try {
            const result = await admin.syncPlaylists();
            await admin.fetchPlaylists();
            syncPlaylistsMsg.value = `✓ ${result.newPlaylists} new playlists, ${result.newSongs} new songs imported`;
        } catch {
            syncPlaylistsMsg.value = 'Sync failed. Check the FPP address.';
        }
        setTimeout(() => (syncPlaylistsMsg.value = null), 5000);
    }

    function playlistEdit(p) {
        if (!playlistEdits.value[p.id]) playlistEdits.value[p.id] = { name: p.name, isEnabled: p.isEnabled };
        return playlistEdits.value[p.id];
    }

    async function savePlaylist(p) {
        playlistSaving.value[p.id] = true;
        const edit = playlistEdits.value[p.id] ?? { name: p.name, isEnabled: p.isEnabled };
        await admin.updatePlaylist(p.id, edit.name, edit.isEnabled);
        delete playlistEdits.value[p.id];
        playlistSaving.value[p.id] = false;
    }

    async function togglePlaylistSongs(p) {
        if (expandedPlaylists.value[p.id]) {
            expandedPlaylists.value[p.id] = false;
            return;
        }
        expandedPlaylists.value[p.id] = true;
        if (!playlistSongs.value[p.id]) {
            playlistSongs.value[p.id] = await admin.fetchPlaylistSongs(p.id);
        }
    }

    function songEdit(song) {
        if (!songEdits.value[song.id]) songEdits.value[song.id] = { title: song.title, artist: song.artist };
        return songEdits.value[song.id];
    }

    async function saveSong(playlistId, song) {
        songSaving.value[song.id] = true;
        const edit = songEdits.value[song.id] ?? { title: song.title, artist: song.artist };
        const updated = await admin.updateSong(song.id, edit.title, edit.artist);
        const list = playlistSongs.value[playlistId];
        if (list) {
            const idx = list.findIndex(s => s.id === song.id);
            if (idx !== -1) list[idx] = { ...list[idx], title: updated.title, artist: updated.artist };
        }
        delete songEdits.value[song.id];
        songSaving.value[song.id] = false;
    }

    async function testMqtt() {
        try {
            await admin.testMqtt();
            mqttMsg.value = 'Test message sent!';
        } catch {
            mqttMsg.value = 'MQTT test failed. Check the broker settings.';
        }
        setTimeout(() => (mqttMsg.value = null), 3000);
    }

    async function skipItem(id) {
        await admin.skipQueueItem(id);
        await showStore.fetchQueue();
    }

    // Chart data
    const donationChartData = computed(() => ({
        labels: admin.reports?.dailyStats.map(d => d.date.slice(5)) ?? [],
        datasets: [
            {
                label: 'Donations ($)',
                data: admin.reports?.dailyStats.map(d => Number(d.donations)) ?? [],
                borderColor: '#22c55e',
                backgroundColor: 'rgba(34,197,94,0.1)',
                fill: true,
                tension: 0.3
            }
        ]
    }));

    const queueChartData = computed(() => ({
        labels: admin.reports?.dailyStats.map(d => d.date.slice(5)) ?? [],
        datasets: [
            {
                label: 'Songs Queued',
                data: admin.reports?.dailyStats.map(d => d.songsQueued) ?? [],
                backgroundColor: 'rgba(99,102,241,0.7)',
                borderRadius: 4
            }
        ]
    }));

    const visitsChartData = computed(() => ({
        labels: admin.reports?.dailyStats.map(d => d.date.slice(5)) ?? [],
        datasets: [
            {
                type: 'bar',
                label: 'Visits',
                data: admin.reports?.dailyStats.map(d => d.visits) ?? [],
                backgroundColor: 'rgba(56,189,248,0.6)',
                borderRadius: 4
            },
            {
                type: 'line',
                label: 'Unique devices',
                data: admin.reports?.dailyStats.map(d => d.devices) ?? [],
                borderColor: '#f472b6',
                backgroundColor: 'rgba(244,114,182,0.1)',
                tension: 0.3
            },
            {
                type: 'line',
                label: 'Listen taps',
                data: admin.reports?.dailyStats.map(d => d.listens) ?? [],
                borderColor: '#4ade80',
                backgroundColor: 'rgba(74,222,128,0.1)',
                tension: 0.3
            }
        ]
    }));

    function fmtDuration(secs) {
        if (!secs) return '0m';
        const h = Math.floor(secs / 3600);
        const m = Math.floor((secs % 3600) / 60);
        return h > 0 ? `${h}h ${m}m` : m > 0 ? `${m}m` : `${secs}s`;
    }
    // Of the devices that visited, how many tapped Listen
    const listenRate = computed(() => {
        const r = admin.reports;
        return r && r.uniqueDevices > 0 ? Math.round((r.listen.devices / r.uniqueDevices) * 100) : 0;
    });

    const resetMsg = ref(null);
    async function resetStats() {
        if (!window.confirm('Reset all visitor and Listen-live statistics and the cheer counter?\n\nPayments, donations and the supporters list are NOT affected. This cannot be undone.')) return;
        try {
            const r = await admin.resetStats();
            resetMsg.value = `Cleared ${r.visits} visits and ${r.listens} listen sessions.`;
        } catch {
            resetMsg.value = 'Reset failed.';
        }
        setTimeout(() => (resetMsg.value = null), 4000);
    }

    const chartOptions = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: {
            x: { ticks: { color: '#6b7280', maxTicksLimit: 10 }, grid: { color: '#1f2937' } },
            y: { ticks: { color: '#6b7280' }, grid: { color: '#1f2937' } }
        }
    };

    async function importFppSchedule() {
        scheduleMsg.value = 'Importing...';
        try {
            const result = await admin.syncSchedule();
            if (result.synced) {
                scheduleMsg.value = `Imported: ${result.daysEnabled} days enabled from ${result.totalEntries} FPP entries.`;
                // Reload config so the editor reflects the new schedule
                await admin.fetchConfig();
                editConfig.value = JSON.parse(JSON.stringify(admin.config));
            } else {
                scheduleMsg.value = result.message;
            }
        } catch {
            scheduleMsg.value = 'Import failed. Check the FPP connection.';
        }
        setTimeout(() => (scheduleMsg.value = null), 4000);
    }

    // Schedule helpers
    const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

    const scheduleEntries = computed({
        get: () => {
            let entries = [];
            try {
                entries = JSON.parse(editConfig.value?.showScheduleJson || '[]');
            } catch {}
            // Ensure all 7 days are present
            return DAYS.map(day => entries.find(e => e.day === day) ?? { day, start: '17:00', end: '22:00', enabled: false });
        },
        set: val => {
            editConfig.value.showScheduleJson = JSON.stringify(val);
        }
    });

    // FPP schedules can use sun-relative times instead of clock times
    const isSunLabel = v => ['dawn', 'sunrise', 'sunset', 'dusk'].includes(String(v ?? '').toLowerCase().replace(/\s/g, ''));
    const offsetText = m => (m ? ` ${m > 0 ? '+' : '-'}${Math.abs(m)} min` : '');
    const needsLocation = computed(() => {
        const c = editConfig.value;
        if (!c || (c.latitude != null && c.longitude != null)) return false;
        return scheduleEntries.value.some(e => e.enabled && (isSunLabel(e.start) || isSunLabel(e.end)));
    });

    function updateScheduleEntry(day, field, value) {
        const entries = scheduleEntries.value.map(e => (e.day === day ? { ...e, [field]: value } : e));
        scheduleEntries.value = entries;
    }

    // Social media helpers
    function addSocialLink() {
        const links = JSON.parse(editConfig.value.socialMediaJson || '[]');
        links.push({ platform: '', url: '' });
        editConfig.value.socialMediaJson = JSON.stringify(links);
    }
    function removeSocialLink(i) {
        const links = JSON.parse(editConfig.value.socialMediaJson || '[]');
        links.splice(i, 1);
        editConfig.value.socialMediaJson = JSON.stringify(links);
    }
    const socialLinks = computed({
        get: () => {
            try {
                return JSON.parse(editConfig.value?.socialMediaJson || '[]');
            } catch {
                return [];
            }
        },
        set: val => {
            editConfig.value.socialMediaJson = JSON.stringify(val);
        }
    });
</script>

<template>
    <main class="mx-auto max-w-5xl px-4 py-8">
        <div class="mb-8 flex items-center justify-between">
            <h1 class="text-2xl font-bold">Admin Dashboard</h1>
            <button @click="logout" class="text-sm font-bold text-red-500 hover:text-white">Logout</button>
        </div>

        <!-- Tabs -->
        <div class="mb-8 flex flex-wrap gap-1 rounded-xl border border-gray-800 bg-gray-900 p-1">
            <button
                v-for="tab in ['reports', 'playlists', 'config', 'queue']"
                :key="tab"
                @click="activeTab = tab"
                class="min-w-0 flex-1 rounded-lg py-2 text-sm capitalize transition"
                :class="activeTab === tab ? 'bg-gray-700 font-medium text-white' : 'text-gray-400 hover:text-white'"
            >
                <template v-if="tab === 'reports'">
                    <font-awesome-icon :icon="['fas', 'chart-bar']" class="ml-1 text-gray-300" />
                    Reports
                </template>
                <template v-if="tab === 'playlists'">
                    <font-awesome-icon :icon="['fas', 'music']" class="ml-1 text-gray-300" />
                    Playlists
                </template>
                <template v-if="tab === 'config'">
                    <font-awesome-icon :icon="['fas', 'cog']" class="ml-1 text-gray-300" />
                    Config
                </template>
                <template v-if="tab === 'queue'">
                    <font-awesome-icon :icon="['fas', 'list-ol']" class="ml-1 text-gray-300" />
                    Queue
                </template>
            </button>
        </div>

        <!-- Reports Tab (includes Ratings) -->
        <div v-if="activeTab === 'reports'">
            <!-- Donation + queue stat cards -->
            <div v-if="admin.reports" class="mb-6 grid grid-cols-2 gap-4 md:grid-cols-4">
                <div class="rounded-xl border border-gray-800 bg-gray-900 p-4">
                    <p class="mb-1 text-xs text-gray-400">Total Donations</p>
                    <p class="text-2xl font-bold text-green-400">${{ Number(admin.reports.totalDonations).toFixed(2) }}</p>
                </div>
                <div class="rounded-xl border border-gray-800 bg-gray-900 p-4">
                    <p class="mb-1 text-xs text-gray-400">Songs Played</p>
                    <p class="text-2xl font-bold text-white">{{ admin.reports.totalSongsPlayed }}</p>
                </div>
                <div class="rounded-xl border border-gray-800 bg-gray-900 p-4">
                    <p class="mb-1 text-xs text-gray-400">Highest Donation</p>
                    <p class="text-2xl font-bold text-yellow-400">${{ Number(admin.reports.highestDonation).toFixed(2) }}</p>
                </div>
                <div class="rounded-xl border border-gray-800 bg-gray-900 p-4">
                    <p class="mb-1 text-xs text-gray-400">Top Song</p>
                    <p class="truncate text-sm font-bold text-white">{{ admin.reports.topSong || '-' }}</p>
                </div>
            </div>

            <!-- Visitor stat cards -->
            <div v-if="admin.reports" class="mb-6 grid grid-cols-3 gap-4">
                <div class="rounded-xl border border-gray-800 bg-gray-900 p-4">
                    <p class="mb-1 text-xs text-gray-400">Visits Today</p>
                    <p class="text-2xl font-bold text-sky-400">{{ admin.reports.visitsToday }}</p>
                </div>
                <div class="rounded-xl border border-gray-800 bg-gray-900 p-4">
                    <p class="mb-1 text-xs text-gray-400">Visits (30d)</p>
                    <p class="text-2xl font-bold text-white">{{ admin.reports.totalVisits }}</p>
                </div>
                <div class="rounded-xl border border-gray-800 bg-gray-900 p-4">
                    <p class="mb-1 text-xs text-gray-400">Unique Devices (30d)</p>
                    <p class="text-2xl font-bold text-pink-400">{{ admin.reports.uniqueDevices }}</p>
                </div>
            </div>

            <!-- Listen live stat cards -->
            <div v-if="admin.reports?.listen" class="mb-6">
                <div class="mb-2 flex items-center justify-between gap-3">
                    <h2 class="text-sm font-semibold tracking-wide text-gray-300 uppercase">Listen live (30d)</h2>
                    <span v-if="admin.reports.listen.listeningNow > 0" class="rounded-full bg-green-500/15 px-2.5 py-0.5 text-xs font-semibold text-green-300">{{ admin.reports.listen.listeningNow }} listening now</span>
                </div>
                <div class="grid grid-cols-2 gap-4 md:grid-cols-5">
                    <div class="rounded-xl border border-gray-800 bg-gray-900 p-4">
                        <p class="mb-1 text-xs text-gray-400">Listen taps</p>
                        <p class="text-2xl font-bold text-green-400">{{ admin.reports.listen.sessions }}</p>
                        <p class="mt-0.5 text-xs text-gray-500">{{ admin.reports.listen.sessionsToday }} today</p>
                    </div>
                    <div class="rounded-xl border border-gray-800 bg-gray-900 p-4">
                        <p class="mb-1 text-xs text-gray-400">Unique listeners</p>
                        <p class="text-2xl font-bold text-white">{{ admin.reports.listen.devices }}</p>
                        <p class="mt-0.5 text-xs text-gray-500">{{ listenRate }}% of visitors</p>
                    </div>
                    <div class="rounded-xl border border-gray-800 bg-gray-900 p-4">
                        <p class="mb-1 text-xs text-gray-400">Avg. listen time</p>
                        <p class="text-2xl font-bold text-white">{{ fmtDuration(admin.reports.listen.averageSeconds) }}</p>
                    </div>
                    <div class="rounded-xl border border-gray-800 bg-gray-900 p-4">
                        <p class="mb-1 text-xs text-gray-400">Total listening</p>
                        <p class="text-2xl font-bold text-white">{{ fmtDuration(admin.reports.listen.totalSeconds) }}</p>
                    </div>
                    <div class="rounded-xl border border-gray-800 bg-gray-900 p-4">
                        <p class="mb-1 text-xs text-gray-400">Peak at once</p>
                        <p class="text-2xl font-bold text-yellow-400">{{ admin.reports.listen.peakConcurrent }}</p>
                    </div>
                </div>
            </div>

            <!-- Charts -->
            <div v-if="admin.reports" class="mb-8 grid gap-6 md:grid-cols-2">
                <div class="rounded-xl border border-gray-800 bg-gray-900 p-5 md:col-span-2">
                    <p class="mb-4 text-sm text-gray-400">Visits, Unique Devices &amp; Listen taps (30d)</p>
                    <div class="h-48">
                        <Bar :data="visitsChartData" :options="chartOptions" />
                    </div>
                </div>
                <div class="rounded-xl border border-gray-800 bg-gray-900 p-5">
                    <p class="mb-4 text-sm text-gray-400">Daily Donations (30d)</p>
                    <div class="h-48">
                        <Line :data="donationChartData" :options="chartOptions" />
                    </div>
                </div>
                <div class="rounded-xl border border-gray-800 bg-gray-900 p-5">
                    <p class="mb-4 text-sm text-gray-400">Songs Queued (30d)</p>
                    <div class="h-48">
                        <Bar :data="queueChartData" :options="chartOptions" />
                    </div>
                </div>
            </div>

            <!-- Reset -->
            <div class="mb-8 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-gray-800 bg-gray-900 p-4">
                <div>
                    <p class="text-sm font-medium text-gray-200">Reset statistics</p>
                    <p class="text-xs text-gray-500">Clears visitor stats, Listen-live stats and the cheer counter. Payments, donations and the supporters list are kept.</p>
                </div>
                <div class="flex items-center gap-3">
                    <span v-if="resetMsg" class="text-xs text-blue-400">{{ resetMsg }}</span>
                    <button @click="resetStats" class="rounded-lg border border-red-500/40 bg-red-500/10 px-4 py-2 text-sm font-medium text-red-300 hover:bg-red-500/20">Reset stats</button>
                </div>
            </div>

            <!-- Song Ratings section -->
            <div class="mb-4 flex items-center justify-between">
                <h2 class="text-sm font-semibold tracking-wide text-gray-300 uppercase">⭐ Song Ratings</h2>
                <button @click="admin.fetchRatings()" class="rounded-lg bg-gray-800 px-3 py-1.5 text-xs text-gray-300 hover:bg-gray-700">Refresh</button>
            </div>

            <div v-if="admin.ratings.length === 0" class="rounded-xl border border-gray-800 bg-gray-900 py-10 text-center text-sm text-gray-400">No ratings yet, guests can rate songs while they play.</div>

            <div v-else>
                <!-- Rating stat cards -->
                <div class="mb-4 grid grid-cols-3 gap-4">
                    <div class="rounded-xl border border-gray-800 bg-gray-900 p-4 text-center">
                        <p class="mb-1 text-xs text-gray-500">Songs Rated</p>
                        <p class="text-2xl font-bold text-white">{{ admin.ratings.length }}</p>
                    </div>
                    <div class="rounded-xl border border-gray-800 bg-gray-900 p-4 text-center">
                        <p class="mb-1 text-xs text-gray-500">Total Votes</p>
                        <p class="text-2xl font-bold text-yellow-400">{{ admin.ratings.reduce((s, r) => s + r.totalRatings, 0) }}</p>
                    </div>
                    <div class="rounded-xl border border-gray-800 bg-gray-900 p-4 text-center">
                        <p class="mb-1 text-xs text-gray-500">Overall Avg</p>
                        <p class="text-2xl font-bold text-green-400">
                            {{ (admin.ratings.reduce((s, r) => s + r.averageRating * r.totalRatings, 0) / admin.ratings.reduce((s, r) => s + r.totalRatings, 0)).toFixed(1) }}
                        </p>
                    </div>
                </div>

                <!-- Ratings table -->
                <div class="overflow-hidden rounded-xl border border-gray-800 bg-gray-900">
                    <div class="grid grid-cols-12 border-b border-gray-800 bg-gray-950 px-4 py-2.5 text-xs tracking-wide text-gray-500 uppercase">
                        <span class="col-span-5">Song</span>
                        <span class="col-span-3">Rating</span>
                        <span class="col-span-2 text-center">Votes</span>
                        <span class="col-span-2 text-right">Breakdown</span>
                    </div>
                    <div v-for="(r, i) in admin.ratings" :key="r.songId" class="grid grid-cols-12 items-center border-b border-gray-800/50 px-4 py-3 last:border-0" :class="i % 2 === 0 ? '' : 'bg-gray-900/50'">
                        <div class="col-span-5 min-w-0 pr-2">
                            <p class="truncate text-sm font-medium text-white">{{ r.title }}</p>
                            <p class="truncate text-xs text-gray-500">{{ r.artist || r.playlistName }}</p>
                        </div>
                        <div class="col-span-3 flex items-center gap-1.5">
                            <span class="text-base leading-none text-yellow-400">
                                <span v-for="n in starsDisplay(r.averageRating).full" :key="'f' + n">★</span>
                                <span v-if="starsDisplay(r.averageRating).half">½</span>
                                <span v-for="n in starsDisplay(r.averageRating).empty" :key="'e' + n" class="opacity-25">★</span>
                            </span>
                            <span class="text-sm font-bold text-white">{{ r.averageRating }}</span>
                        </div>
                        <div class="col-span-2 text-center">
                            <span class="text-sm text-gray-400">{{ r.totalRatings }}</span>
                        </div>
                        <div class="col-span-2 flex flex-col items-end gap-0.5">
                            <div v-for="(count, si) in [r.fiveStars, r.fourStars, r.threeStars, r.twoStars, r.oneStar]" :key="si" class="flex w-full items-center justify-end gap-1">
                                <div class="h-1.5 rounded-full bg-yellow-500/60" :style="`width: ${r.totalRatings > 0 ? (count / r.totalRatings) * 48 : 0}px`" />
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Playlists Tab -->
        <div v-if="activeTab === 'reports'" class="mt-6 rounded-xl border border-gray-800 bg-gray-900 p-5">
            <div class="mb-3 flex items-center justify-between gap-3">
                <h3 class="text-sm font-semibold tracking-wide text-gray-300 uppercase">Supporters list</h3>
                <span class="text-xs" :class="editConfig?.showSupporters ? 'text-green-400' : 'text-gray-500'">{{ editConfig?.showSupporters ? 'Public' : 'Hidden (turn on in Config → Pricing)' }}</span>
            </div>
            <p class="mb-3 text-xs text-gray-500">Latest 200 payments. Hiding a name makes that entry Anonymous; the payment itself is kept.</p>
            <div v-if="!admin.supporters.length" class="py-6 text-center text-sm text-gray-500">No payments yet.</div>
            <div v-else class="max-h-96 divide-y divide-gray-800 overflow-y-auto">
                <div v-for="s in admin.supporters" :key="s.id" class="flex items-center gap-3 py-2 text-sm">
                    <span class="min-w-0 flex-1 truncate" :class="s.name ? 'text-gray-100' : 'text-gray-500 italic'">{{ s.name || 'Anonymous' }}</span>
                    <span class="w-16 shrink-0 text-xs text-gray-500 capitalize">{{ s.kind }}</span>
                    <span class="w-16 shrink-0 text-right text-gray-300 tabular-nums">${{ Number(s.amount).toFixed(2) }}</span>
                    <span class="hidden w-28 shrink-0 text-right text-xs text-gray-500 sm:block">{{ new Date(s.at).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' }) }}</span>
                    <button v-if="s.name" @click="admin.hideSupporterName(s.id)" class="shrink-0 rounded-md border border-gray-700 px-2 py-1 text-xs text-gray-300 hover:border-red-500/50 hover:text-red-300">Hide name</button>
                    <span v-else class="w-[70px] shrink-0" />
                </div>
            </div>
        </div>

        <div v-if="activeTab === 'playlists'">
            <div v-if="admin.playlists.length === 0" class="rounded-xl border border-gray-800 bg-gray-900 py-10 text-center">
                <p class="mb-1 font-medium text-gray-400">No playlists imported yet</p>
                <p class="text-sm text-gray-600">
                    Go to Config → Falcon Player and click
                    <strong class="text-gray-400">Import Playlists</strong>
                    to pull in playlists from your Falcon Player instance.
                </p>
            </div>

            <div class="space-y-3">
                <div v-for="p in admin.playlists" :key="p.id" class="rounded-xl border border-gray-800 bg-gray-900">
                    <!-- Playlist header row -->
                    <div class="flex flex-col gap-3 p-4 md:flex-row md:items-start">
                        <div class="min-w-0 flex-1">
                            <input
                                :value="(playlistEdits[p.id] ?? p).name"
                                @input="
                                    e => {
                                        playlistEdit(p).name = e.target.value;
                                    }
                                "
                                class="input w-full"
                            />
                            <p class="mt-1 truncate text-xs text-gray-400">FPP: {{ p.fppPlaylistName }} · {{ p.songCount }} songs</p>
                        </div>
                        <div class="flex shrink-0 items-center gap-4">
                            <label class="flex cursor-pointer items-center gap-2 text-sm text-gray-300">
                                <input
                                    type="checkbox"
                                    :checked="(playlistEdits[p.id] ?? p).isEnabled"
                                    @change="
                                        e => {
                                            playlistEdit(p).isEnabled = e.target.checked;
                                        }
                                    "
                                    class="accent-green-500"
                                />
                                Visible to users
                            </label>
                            <button @click="savePlaylist(p)" :disabled="playlistSaving[p.id]" class="rounded-lg bg-green-600 px-3 py-1.5 text-sm text-white hover:bg-green-500 disabled:opacity-40">
                                {{ playlistSaving[p.id] ? '...' : 'Save' }}
                            </button>
                            <button @click="togglePlaylistSongs(p)" class="rounded-lg bg-gray-700 px-3 py-1.5 text-sm text-gray-300 hover:bg-gray-600">
                                {{ expandedPlaylists[p.id] ? 'Hide Songs' : 'Edit Songs' }}
                            </button>
                        </div>
                    </div>

                    <!-- Song list (expandable) -->
                    <div v-if="expandedPlaylists[p.id]" class="border-t border-gray-800 px-4 pb-4 pt-3">
                        <p v-if="!playlistSongs[p.id]" class="text-sm text-gray-500">Loading...</p>
                        <p v-else-if="playlistSongs[p.id].length === 0" class="text-sm text-gray-500">No songs in this playlist.</p>
                        <div v-else class="space-y-2">
                            <div
                                v-for="song in playlistSongs[p.id]"
                                :key="song.id"
                                class="flex flex-col gap-2 rounded-lg border border-gray-700 bg-gray-800 px-3 py-2 md:flex-row md:items-center"
                            >
                                <div class="grid min-w-0 flex-1 grid-cols-1 gap-2 md:grid-cols-2">
                                    <input
                                        :value="(songEdits[song.id] ?? song).title"
                                        @input="e => { songEdit(song).title = e.target.value; }"
                                        placeholder="Song title"
                                        class="input text-sm"
                                    />
                                    <input
                                        :value="(songEdits[song.id] ?? song).artist"
                                        @input="e => { songEdit(song).artist = e.target.value; }"
                                        placeholder="Artist"
                                        class="input text-sm"
                                    />
                                </div>
                                <div class="flex shrink-0 items-center gap-2">
                                    <span class="hidden text-xs text-gray-500 md:block">{{ song.filename }}</span>
                                    <button
                                        @click="saveSong(p.id, song)"
                                        :disabled="songSaving[song.id] || !songEdits[song.id]"
                                        class="rounded-lg bg-indigo-600 px-3 py-1.5 text-xs text-white hover:bg-indigo-500 disabled:opacity-40"
                                    >
                                        {{ songSaving[song.id] ? '...' : 'Save' }}
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Config Tab -->
        <div v-if="activeTab === 'config' && editConfig">
            <form @submit.prevent="saveConfig" class="space-y-6">
                <div v-if="editConfig.showMode !== 'auto'" class="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-yellow-500/50 bg-yellow-500/10 p-4">
                    <p class="text-sm text-yellow-100">
                        <strong>Manual override is {{ editConfig.showMode === 'on' ? 'FORCE ON' : 'FORCE OFF' }}.</strong>
                        {{ editConfig.showMode === 'on' ? 'The season dates and weekly hours are being ignored, so the show (and the default playlist) runs right now.' : 'The show is closed regardless of dates or hours.' }}
                    </p>
                    <button type="button" @click="chooseMode('auto')" class="rounded-lg bg-yellow-400 px-4 py-2 text-sm font-semibold text-black hover:bg-yellow-300">Back to Automatic</button>
                </div>

                <!-- Season & manual override -->
                <section class="rounded-xl border border-gray-700 bg-gray-900 p-5">
                    <div class="mb-1 flex items-center justify-between gap-3">
                        <h3 class="text-sm font-semibold tracking-wide text-gray-200 uppercase">Show status</h3>
                        <span class="rounded-full px-2.5 py-0.5 text-xs font-semibold" :class="seasonLive ? 'bg-green-500/15 text-green-300' : 'bg-red-500/15 text-red-300'">
                            {{ seasonLive ? 'Live to visitors' : 'Closed to visitors' }}
                        </span>
                    </div>
                    <p class="mb-4 text-xs text-gray-400">{{ modeHelp }}</p>

                    <div class="mb-1 inline-flex overflow-hidden rounded-lg border border-gray-700" role="group" aria-label="Manual override">
                        <button
                            v-for="m in modeOptions"
                            :key="m.value"
                            type="button"
                            @click="chooseMode(m.value)"
                            class="px-4 py-2 text-sm font-medium transition"
                            :class="editConfig.showMode === m.value ? 'bg-gray-100 text-gray-900' : 'bg-gray-800 text-gray-300 hover:bg-gray-700'"
                        >
                            {{ m.label }}
                        </button>
                    </div>
                    <p v-if="modeMsg" class="mt-1 text-xs text-blue-400">{{ modeMsg }}</p>

                    <div class="mt-5 grid gap-4 sm:grid-cols-2">
                        <label class="block">
                            <span class="text-xs text-gray-400">Season starts</span>
                            <input type="date" v-model="editConfig.seasonStart" class="input mt-1" />
                        </label>
                        <label class="block">
                            <span class="text-xs text-gray-400">Season ends</span>
                            <input type="date" v-model="editConfig.seasonEnd" :min="editConfig.seasonStart || undefined" class="input mt-1" />
                        </label>
                    </div>
                    <p class="mt-2 text-xs text-gray-500">
                        In Automatic mode the show only runs between these dates (inclusive, server time) and inside your weekly hours. Leave a date empty for no limit. Dates are saved with the Save button below.
                        MQTT: publish <span class="font-mono">on</span>, <span class="font-mono">off</span> or <span class="font-mono">auto</span> to <span class="font-mono">{{ editConfig.mqttTopicPrefix || 'qtm' }}/control/show</span>.
                    </p>

                    <label class="mt-4 block">
                        <span class="text-xs text-gray-400">Message shown to visitors when the show is closed</span>
                        <textarea v-model="editConfig.offSeasonMessage" rows="2" class="input mt-1 resize-none" placeholder="The holiday light show is not running right now. Check back soon!" />
                    </label>
                </section>

                <!-- FPP -->
                <section class="rounded-xl border border-gray-800 bg-gray-900 p-5">
                    <h3 class="mb-4 text-sm font-semibold tracking-wide text-gray-300 uppercase">Falcon Player (FPP)</h3>

                    <!-- Connection -->
                    <div class="grid gap-4 md:grid-cols-2">
                        <label class="block">
                            <span class="text-xs text-gray-400">FPP Address</span>
                            <input v-model="editConfig.fppAddress" class="input mt-1" placeholder="192.168.1.100" />
                        </label>
                        <label class="block">
                            <span class="text-xs text-gray-400">Default Playlist Name</span>
                            <select v-if="admin.playlists.length" v-model="editConfig.defaultPlaylistName" class="input mt-1">
                                <option value="">None</option>
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
                    <div class="mt-4 flex gap-6">
                        <label class="flex cursor-pointer items-center gap-2 text-sm text-gray-300">
                            <input type="checkbox" v-model="editConfig.autoPlayDefault" class="accent-green-500" />
                            Auto-play default playlist
                        </label>
                        <label class="flex cursor-pointer items-center gap-2 text-sm text-gray-300">
                            <input type="checkbox" v-model="editConfig.interruptForUserSongs" class="accent-green-500" />
                            Interrupt for user songs
                        </label>
                    </div>

                    <!-- Import section -->
                    <div class="mt-5 border-t border-gray-800 pt-4">
                        <p class="mb-3 text-xs font-semibold tracking-wide text-gray-400 uppercase">Import from FPP</p>
                        <p class="mb-4 text-xs text-gray-500">Save your FPP address above first, then import your playlists and songs. Re-run after adding new sequences in FPP.</p>
                        <div class="flex flex-wrap gap-3">
                            <div class="flex flex-col gap-1">
                                <button type="button" @click="syncPlaylists" class="rounded-lg px-4 py-2.5 text-sm font-medium text-white transition" style="background: linear-gradient(135deg, #312e81, #4338ca); border: 1px solid rgba(99, 102, 241, 0.4)">
                                    <font-awesome-icon :icon="['fas', 'rotate']" class="text-gray-300" />
                                    Import Playlists
                                </button>
                                <p v-if="syncPlaylistsMsg" class="px-1 text-xs" :class="syncPlaylistsMsg.startsWith('✓') ? 'text-green-400' : syncPlaylistsMsg === 'Syncing...' ? 'text-gray-400' : 'text-red-400'">
                                    {{ syncPlaylistsMsg }}
                                </p>
                            </div>
                            <div class="flex flex-col gap-1">
                                <button type="button" @click="syncSongs" class="rounded-lg px-4 py-2.5 text-sm font-medium text-white transition" style="background: linear-gradient(135deg, #1e3a5f, #1e40af); border: 1px solid rgba(59, 130, 246, 0.4)">
                                    <font-awesome-icon :icon="['fas', 'rotate']" class="text-gray-300" />
                                    Import Sequences
                                </button>
                                <p v-if="syncSongsMsg" class="px-1 text-xs" :class="syncSongsMsg.startsWith('✓') ? 'text-green-400' : syncSongsMsg === 'Syncing...' ? 'text-gray-400' : 'text-red-400'">
                                    {{ syncSongsMsg }}
                                </p>
                            </div>
                        </div>
                    </div>
                </section>

                <!-- Site -->
                <section class="rounded-xl border border-gray-800 bg-gray-900 p-5">
                    <h3 class="mb-4 text-sm font-semibold tracking-wide text-gray-300 uppercase">Site</h3>
                    <div class="grid gap-4 md:grid-cols-2">
                        <label class="block">
                            <span class="text-xs text-gray-400">Site Name</span>
                            <input v-model="editConfig.siteName" class="input mt-1" />
                        </label>
                        <label class="block">
                            <span class="text-xs text-gray-400">FM Radio Station</span>
                            <input v-model="editConfig.fmRadioStation" class="input mt-1" placeholder="e.g. 90.5 FM" />
                        </label>
                    </div>
                </section>

                <!-- Weekly Schedule -->
                <section class="rounded-xl border border-gray-800 bg-gray-900 p-5">
                    <div class="mb-1 flex items-start justify-between gap-4">
                        <div>
                            <h3 class="text-sm font-semibold tracking-wide text-gray-300 uppercase">Weekly Show Schedule</h3>
                            <p class="mt-1 text-xs text-gray-400">Displayed on the public Info page. Import directly from your FPP scheduler.</p>
                        </div>
                        <button type="button" @click="importFppSchedule" class="shrink-0 rounded-lg px-3 py-1.5 text-sm whitespace-nowrap text-white transition" style="background: linear-gradient(135deg, #1e3a5f, #1e40af); border: 1px solid rgba(59, 130, 246, 0.4)">
                            📅 Import from FPP
                        </button>
                    </div>
                    <p v-if="scheduleMsg" class="mb-3 text-xs text-blue-400">{{ scheduleMsg }}</p>
                    <div v-else class="mb-4" />

                    <div v-if="needsLocation" class="mb-4 rounded-lg border border-yellow-500/40 bg-yellow-500/10 p-3 text-xs text-yellow-200">
                        This schedule uses Dawn / Sunrise / Sunset / Dusk, but no location is set, so the show is treated as <strong>closed</strong> (the default playlist will not start). Enter your latitude and longitude below, or import from FPP again.
                    </div>
                    <div class="mb-4 grid gap-3 sm:grid-cols-2">
                        <label class="block">
                            <span class="text-xs text-gray-400">Latitude (for Dusk / Sunset times)</span>
                            <input type="number" step="any" min="-90" max="90" v-model.number="editConfig.latitude" class="input mt-1" placeholder="e.g. 41.88" />
                        </label>
                        <label class="block">
                            <span class="text-xs text-gray-400">Longitude</span>
                            <input type="number" step="any" min="-180" max="180" v-model.number="editConfig.longitude" class="input mt-1" placeholder="e.g. -87.63" />
                        </label>
                    </div>
                    <div class="space-y-2">
                        <div v-for="entry in scheduleEntries" :key="entry.day" class="flex items-center gap-3 overflow-auto rounded-lg border px-3 py-2.5" :class="entry.enabled ? 'border-gray-700 bg-gray-800' : 'border-gray-800 bg-gray-900'">
                            <!-- Enable toggle -->
                            <input type="checkbox" :checked="entry.enabled" @change="e => updateScheduleEntry(entry.day, 'enabled', e.target.checked)" class="shrink-0 accent-green-500" />
                            <!-- Day name -->
                            <span class="w-24 shrink-0 text-sm" :class="entry.enabled ? 'font-medium text-white' : 'text-gray-500'">
                                {{ entry.day }}
                            </span>
                            <!-- Times -->
                            <div class="flex flex-1 items-center gap-2" :class="entry.enabled ? '' : 'pointer-events-none opacity-40'">
                                <template v-for="(field, idx) in ['start', 'end']" :key="field">
                                    <span v-if="idx === 1" class="shrink-0 text-xs text-gray-500">to</span>
                                    <button v-if="isSunLabel(entry[field])" type="button" @click="updateScheduleEntry(entry.day, field, field === 'start' ? '17:00' : '22:00')" title="Click to use a fixed clock time instead" class="input flex-1 py-1.5 text-left text-xs text-yellow-300">
                                        {{ entry[field] }}{{ offsetText(entry[field === 'start' ? 'startOffset' : 'endOffset']) }}
                                    </button>
                                    <input v-else type="time" :value="entry[field]" @change="e => updateScheduleEntry(entry.day, field, e.target.value)" class="input flex-1 py-1.5 text-xs" />
                                </template>
                            </div>
                            <span v-if="!entry.enabled" class="shrink-0 text-xs text-gray-600">Closed</span>
                        </div>
                    </div>
                </section>

                <!-- Pricing -->
                <section class="rounded-xl border border-gray-800 bg-gray-900 p-5">
                    <h3 class="mb-4 text-sm font-semibold tracking-wide text-gray-300 uppercase">Pricing</h3>
                    <div class="grid gap-4 md:grid-cols-2">
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

                    <h4 class="mt-6 mb-1 text-xs font-semibold tracking-wide text-gray-300 uppercase">Season donation goal</h4>
                    <p class="mb-3 text-xs text-gray-500">Shown on the home page as a row of bulbs that light up as you get closer. Counts every payment (requests, boosts and donations) from 30 days before the season start date (or the last year if no start date is set). Set to 0 to hide it.</p>
                    <div class="grid gap-4 md:grid-cols-2">
                        <label class="block">
                            <span class="text-xs text-gray-400">Goal ($)</span>
                            <input type="number" step="1" min="0" v-model.number="editConfig.donationGoal" class="input mt-1" />
                        </label>
                        <label class="block">
                            <span class="text-xs text-gray-400">What it's for</span>
                            <input type="text" maxlength="120" v-model="editConfig.donationGoalLabel" class="input mt-1" placeholder="Help us add 2,000 new lights next year" />
                        </label>
                    </div>

                    <label class="mt-6 flex cursor-pointer items-start gap-3">
                        <input type="checkbox" v-model="editConfig.showSupporters" class="mt-0.5 h-4 w-4 accent-green-500" />
                        <span>
                            <span class="block text-sm font-medium text-gray-200">Public supporters list</span>
                            <span class="block text-xs text-gray-500">Shows this season's requests, boosts and donations with amounts to everyone. Visitors choose whether to show their name; the default is Anonymous. You can hide any name from the Reports tab.</span>
                        </span>
                    </label>
                </section>

                <!-- Rate Limits -->
                <section class="rounded-xl border border-gray-800 bg-gray-900 p-5">
                    <h3 class="mb-4 text-sm font-semibold tracking-wide text-gray-300 uppercase">Rate Limits</h3>
                    <div class="grid gap-4 md:grid-cols-2">
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

                <!-- Friends & family -->
                <section class="rounded-xl border border-gray-800 bg-gray-900 p-5">
                    <h3 class="mb-1 text-sm font-semibold tracking-wide text-gray-300 uppercase">Friends &amp; family</h3>
                    <p class="mb-4 text-xs text-gray-500">
                        Give people a personal code. They sign in on the site and can see the show and request songs for <strong>free, at any time</strong>, even before the season opens or outside show hours (those songs play on your lights). Delete a pass to cut that person off.
                    </p>
                    <div class="mb-4 grid gap-4 md:grid-cols-2">
                        <label class="flex cursor-pointer items-center gap-3">
                            <input type="checkbox" v-model="editConfig.guestPassesEnabled" class="h-4 w-4 accent-green-500" />
                            <span class="text-sm text-gray-200">Allow friends &amp; family sign-in</span>
                        </label>
                        <label class="block">
                            <span class="text-xs text-gray-400">Free songs per guest, per rate-limit window</span>
                            <input type="number" min="1" max="200" v-model.number="editConfig.guestMaxSongsPerWindow" class="input mt-1" />
                        </label>
                    </div>

                    <div class="mb-3 flex gap-2">
                        <input v-model="newGuestName" @keydown.enter.prevent="createGuest" type="text" maxlength="30" placeholder="Guest name, e.g. Grandma Sue" class="input flex-1" />
                        <button type="button" @click="createGuest" :disabled="!newGuestName.trim()" class="shrink-0 rounded-lg bg-gray-100 px-4 py-2 text-sm font-medium text-gray-900 disabled:opacity-40">Add pass</button>
                    </div>
                    <p v-if="guestMsg" class="mb-2 text-xs text-blue-400">{{ guestMsg }}</p>

                    <div v-if="!admin.guests.length" class="py-4 text-center text-sm text-gray-500">No passes yet.</div>
                    <div v-else class="divide-y divide-gray-800">
                        <div v-for="g in admin.guests" :key="g.id" class="flex flex-wrap items-center gap-3 py-2.5 text-sm">
                            <span class="min-w-0 flex-1 truncate text-gray-100">{{ g.name }}</span>
                            <span class="rounded bg-gray-800 px-2 py-1 font-mono text-sm tracking-widest text-yellow-300">{{ g.code }}</span>
                            <span class="hidden w-28 text-right text-xs text-gray-500 sm:block">{{ g.requestCount }} requests</span>
                            <button type="button" @click="copyGuest(g, 'code')" class="rounded-md border border-gray-700 px-2 py-1 text-xs text-gray-300 hover:bg-gray-800">Copy code</button>
                            <button type="button" @click="copyGuest(g, 'link')" class="rounded-md border border-gray-700 px-2 py-1 text-xs text-gray-300 hover:bg-gray-800">Copy link</button>
                            <button type="button" @click="removeGuest(g)" class="rounded-md border border-gray-700 px-2 py-1 text-xs text-gray-300 hover:border-red-500/50 hover:text-red-300">Delete</button>
                        </div>
                    </div>
                </section>

                <!-- Listen live -->
                <section class="rounded-xl border border-gray-800 bg-gray-900 p-5">
                    <div class="mb-1 flex items-center justify-between gap-3">
                        <h3 class="text-sm font-semibold tracking-wide text-gray-300 uppercase">Listen live (phone audio)</h3>
                        <span v-if="listenStatus" class="rounded-full px-2.5 py-0.5 text-xs font-semibold" :class="listenStatus.live ? 'bg-green-500/15 text-green-300' : 'bg-gray-700 text-gray-400'">
                            {{ listenStatus.live ? `Receiving audio · ${listenStatus.listeners} listening` : 'No audio from FPP' }}
                        </span>
                    </div>
                    <p class="mb-4 text-xs text-gray-500">
                        Visitors tap <em>Listen</em> and hear the show on their phone, about 0.15 to 0.3 seconds behind the lights. Audio goes through this site's normal address, so no port forwarding is needed. The button only appears while audio is arriving.
                        In FPP, enable <strong>Opus RTP Audio Streaming</strong> and send it to this server's LAN IP on UDP port {{ listenStatus?.port ?? 5004 }} (don't port-forward it).
                        <a href="https://github.com/thebradleysanders/QueueTheMagic/blob/main/docs/LISTEN.md" target="_blank" rel="noopener" class="text-blue-400 hover:underline">Setup guide</a>
                    </p>
                    <div class="grid gap-4 md:grid-cols-2">
                        <label class="flex cursor-pointer items-center gap-3">
                            <input type="checkbox" v-model="editConfig.listenEnabled" class="h-4 w-4 accent-green-500" />
                            <span class="text-sm text-gray-200">Show the Listen button</span>
                        </label>
                        <label class="block">
                            <span class="text-xs text-gray-400">Max listeners (each uses about 64 kbps of your upload)</span>
                            <input type="number" min="1" max="500" v-model.number="editConfig.listenMaxListeners" class="input mt-1" />
                        </label>
                    </div>
                </section>

                <!-- MQTT -->
                <section class="rounded-xl border border-gray-800 bg-gray-900 p-5">
                    <h3 class="mb-4 text-sm font-semibold tracking-wide text-gray-300 uppercase">MQTT</h3>
                    <p class="mb-4 text-xs leading-relaxed text-gray-400">
                        Optional. Publishes events (song requested, donation, now playing) to an MQTT broker so other tools, such as Home Assistant, phone alerts or a roadside sign, can react to them. The show works the same with this off.
                        <a href="https://github.com/thebradleysanders/QueueTheMagic/blob/main/docs/MQTT.md" target="_blank" rel="noopener" class="text-blue-400 underline">Read the MQTT guide</a>
                    </p>
                    <label class="mb-4 flex cursor-pointer items-center gap-2 text-sm text-gray-300">
                        <input type="checkbox" v-model="editConfig.mqttEnabled" class="accent-green-500" />
                        Enable MQTT
                    </label>
                    <div v-if="editConfig.mqttEnabled" class="grid gap-4 md:grid-cols-2">
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
                        <button type="button" @click="testMqtt" class="rounded-lg bg-gray-700 px-4 py-2 text-sm text-white hover:bg-gray-600">Send Test Message</button>
                        <p v-if="mqttMsg" class="mt-2 text-xs text-green-400">{{ mqttMsg }}</p>
                    </div>
                </section>

                <!-- Social Media -->
                <section class="md:mb-16 rounded-xl border border-gray-800 bg-gray-900 p-5">
                    <h3 class="mb-4 text-sm font-semibold tracking-wide text-gray-300 uppercase">Social Media</h3>
                    <div v-for="(link, i) in socialLinks" :key="i" class="mb-2 flex gap-2">
                        <input v-model="link.platform" class="input flex-1" placeholder="Platform (e.g. facebook)" @input="socialLinks = [...socialLinks]" />
                        <input v-model="link.url" class="input flex-2 grow" placeholder="https://..." @input="socialLinks = [...socialLinks]" />
                        <button type="button" @click="removeSocialLink(i)" class="px-2 text-red-500 hover:text-red-400">
                            <font-awesome-icon :icon="['fas', 'xmark']" />
                        </button>
                    </div>
                    <button type="button" @click="addSocialLink" class="mt-2 text-sm text-green-400 hover:text-green-300">+ Add link</button>
                </section>

                <div class="md:fixed bottom-5  flex items-center justify-center gap-4 border-t border-gray-800 bg-gray-900/95 py-3 right-5 md:justify-end px-8 rounded-xl">
                    <div class="flex items-center gap-2">
                        <button type="submit" :disabled="saving" class="rounded-xl bg-green-600 px-6 py-2.5 font-semibold text-white hover:bg-green-500 disabled:opacity-50">
                            {{ saving ? 'Saving...' : 'Save Configuration' }}
                        </button>
                        <span v-if="saveMsg" class="text-sm text-green-400">{{ saveMsg }}</span>
                    </div>

                    <button type="button" @click="goHome" class="text-sm text-gray-400 hover:text-gray-300">Cancel</button>
                </div>
            </form>
        </div>

        <!-- Queue Tab -->
        <div v-if="activeTab === 'queue'">
            <!-- Diagnostics -->
            <div class="mb-6 rounded-xl border border-gray-800 bg-gray-900 p-5" v-if="admin.diagnostics">
                <h3 class="mb-3 text-sm font-semibold tracking-wide text-gray-300 uppercase">State Machine</h3>
                <div class="mb-4 flex gap-4">
                    <div>
                        <span class="text-xs text-gray-500">State</span>
                        <p class="font-mono font-bold text-green-400">{{ admin.diagnostics.state }}</p>
                    </div>
                    <div>
                        <span class="text-xs text-gray-500">Sync</span>
                        <p class="font-mono text-white">{{ admin.diagnostics.syncStatus }}</p>
                    </div>
                    <div>
                        <span class="text-xs text-gray-500">Active Item</span>
                        <p class="font-mono text-white">{{ admin.diagnostics.activeQueueItemId ?? '-' }}</p>
                    </div>
                </div>

                <div class="mt-4">
                    <p class="mb-2 text-xs text-gray-500">FPP Command Log (last 30)</p>
                    <div class="max-h-48 space-y-1 overflow-y-auto rounded-lg bg-gray-950 p-3 font-mono text-xs">
                        <div v-for="(cmd, i) in admin.diagnostics.commandLog.slice().reverse()" :key="i" class="flex gap-2 text-gray-400">
                            <span class="text-gray-600">{{ cmd.timestamp?.slice(11, 19) }}</span>
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
            <div class="rounded-xl border border-gray-800 bg-gray-900 p-5">
                <h3 class="mb-3 text-sm font-semibold tracking-wide text-gray-300 uppercase">Live Queue</h3>
                <div v-if="showStore.queue.length === 0" class="text-sm text-gray-500">Queue is empty.</div>
                <div v-else class="space-y-2">
                    <div v-for="item in showStore.queue" :key="item.id" class="flex items-center gap-3 rounded-lg bg-gray-950 px-4 py-2.5">
                        <span class="w-6 text-sm text-gray-600">{{ item.position }}</span>
                        <div class="min-w-0 flex-1">
                            <p class="text-sm font-medium text-white">{{ item.title }}</p>
                            <p class="text-xs text-gray-500">{{ item.artist }} · {{ item.status }}</p>
                        </div>
                        <button @click="skipItem(item.id)" class="rounded-lg bg-red-500/10 px-3 py-1 text-xs text-red-400 hover:text-red-300">Skip</button>
                    </div>
                </div>
            </div>
        </div>
    </main>
</template>

<style scoped>
    @reference "tailwindcss";
    .input {
        @apply w-full rounded-lg border border-gray-700 bg-gray-800 px-3 py-2 text-sm text-white focus:border-green-500 focus:outline-none;
    }
</style>
