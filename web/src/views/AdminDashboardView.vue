<script setup>
    import { BarElement, CategoryScale, Chart as ChartJS, Filler, Legend, LineElement, LinearScale, PointElement, Title, Tooltip } from 'chart.js';
    import { Bar, Line } from 'vue-chartjs';
    import { useRouter } from 'vue-router';

    import { computed, onMounted, ref } from 'vue';

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

    onMounted(async () => {
        await Promise.all([admin.fetchReports(), admin.fetchRatings(), admin.fetchConfig(), admin.fetchDiagnostics(), admin.fetchPlaylists()]);
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
            await admin.saveConfig(editConfig.value);
            saveMsg.value = 'Saved!';
            setTimeout(() => (saveMsg.value = null), 2000);
        } catch {
            saveMsg.value = 'Save failed.';
        } finally {
            saving.value = false;
        }
    }

    async function syncSongs() {
        syncSongsMsg.value = 'Syncing...';
        try {
            const result = await admin.syncSongs();
            syncSongsMsg.value = `✓ ${result.synced} new songs synced (${result.total} total sequences found)`;
        } catch {
            syncSongsMsg.value = 'Sync failed — check FPP address.';
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
            syncPlaylistsMsg.value = 'Sync failed — check FPP address.';
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

    async function testMqtt() {
        try {
            await admin.testMqtt();
            mqttMsg.value = 'Test message sent!';
        } catch {
            mqttMsg.value = 'MQTT test failed — check broker settings.';
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
                scheduleMsg.value = `Imported — ${result.daysEnabled} days enabled from ${result.totalEntries} FPP entries.`;
                // Reload config so the editor reflects the new schedule
                await admin.fetchConfig();
                editConfig.value = JSON.parse(JSON.stringify(admin.config));
            } else {
                scheduleMsg.value = result.message;
            }
        } catch {
            scheduleMsg.value = 'Import failed — check FPP connection.';
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
                    <p class="truncate text-sm font-bold text-white">{{ admin.reports.topSong || '—' }}</p>
                </div>
            </div>

            <!-- Charts -->
            <div v-if="admin.reports" class="mb-8 grid gap-6 md:grid-cols-2">
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
        <div v-if="activeTab === 'playlists'">
            <div v-if="admin.playlists.length === 0" class="rounded-xl border border-gray-800 bg-gray-900 py-10 text-center">
                <p class="mb-1 font-medium text-gray-400">No playlists imported yet</p>
                <p class="text-sm text-gray-600">
                    Go to Config → Falcon Player and click
                    <strong class="text-gray-400">Import Playlists</strong>
                    .
                </p>
            </div>

            <div class="space-y-3">
                <div v-for="p in admin.playlists" :key="p.id" class="flex flex-col gap-3 rounded-xl border border-gray-800 bg-gray-900 p-4 md:flex-row md:items-center">
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
                    </div>
                </div>
            </div>
        </div>

        <!-- Config Tab -->
        <div v-if="activeTab === 'config' && editConfig">
            <form @submit.prevent="saveConfig" class="space-y-6">
                <!-- Season toggle -->
                <section class="rounded-xl border p-5" :class="editConfig.isSeasonActive ? 'border-green-500/30 bg-green-500/5' : 'border-red-500/30 bg-red-500/5'">
                    <div class="flex items-center justify-between gap-4">
                        <div>
                            <h3 class="text-sm font-semibold tracking-wide uppercase" :class="editConfig.isSeasonActive ? 'text-green-300' : 'text-red-300'">
                                {{ editConfig.isSeasonActive ? '🟢 Season Active' : '🔴 Season Inactive' }}
                            </h3>
                            <p class="mt-1 text-xs text-gray-400">
                                {{ editConfig.isSeasonActive ? 'The public site is live. Visitors can request songs.' : 'The public site shows an off-season message. No requests accepted.' }}
                            </p>
                        </div>
                        <button
                            type="button"
                            @click="editConfig.isSeasonActive = !editConfig.isSeasonActive"
                            class="shrink-0 rounded-xl px-5 py-2.5 text-sm font-bold transition"
                            :class="editConfig.isSeasonActive ? 'border border-red-500/30 bg-red-500/15 text-red-300 hover:bg-red-500/25' : 'border border-green-500/30 bg-green-500/15 text-green-300 hover:bg-green-500/25'"
                        >
                            {{ editConfig.isSeasonActive ? 'Turn Off' : 'Turn On' }}
                        </button>
                    </div>
                    <div v-if="!editConfig.isSeasonActive" class="mt-4">
                        <label class="block">
                            <span class="text-xs text-gray-400">Off-season message shown to visitors</span>
                            <textarea v-model="editConfig.offSeasonMessage" rows="2" class="input mt-1 resize-none" placeholder="The holiday light show is not running right now. Check back soon!" />
                        </label>
                    </div>
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
                        <p class="mb-3 text-xs font-semibold tracking-wide text-gray-500 uppercase">Import from FPP</p>
                        <p class="mb-4 text-xs text-gray-600">Save your FPP address above first, then import your playlists and songs. Re-run after adding new sequences in FPP.</p>
                        <div class="flex flex-wrap gap-3">
                            <div class="flex flex-col gap-1">
                                <button type="button" @click="syncPlaylists" class="rounded-lg px-4 py-2.5 text-sm font-medium text-white transition" style="background: linear-gradient(135deg, #312e81, #4338ca); border: 1px solid rgba(99, 102, 241, 0.4)">
                                    <font-awesome-icon :icon="['fas', 'sync']" class="text-gray-300" />
                                    Import Playlists
                                </button>
                                <p v-if="syncPlaylistsMsg" class="px-1 text-xs" :class="syncPlaylistsMsg.startsWith('✓') ? 'text-green-400' : syncPlaylistsMsg === 'Syncing...' ? 'text-gray-400' : 'text-red-400'">
                                    {{ syncPlaylistsMsg }}
                                </p>
                            </div>
                            <div class="flex flex-col gap-1">
                                <button type="button" @click="syncSongs" class="rounded-lg px-4 py-2.5 text-sm font-medium text-white transition" style="background: linear-gradient(135deg, #1e3a5f, #1e40af); border: 1px solid rgba(59, 130, 246, 0.4)">
                                    <font-awesome-icon :icon="['fas', 'sync']" class="text-gray-300" />
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
                                <input type="time" :value="entry.start" @change="e => updateScheduleEntry(entry.day, 'start', e.target.value)" class="input flex-1 py-1.5 text-xs" />
                                <span class="shrink-0 text-xs text-gray-500">to</span>
                                <input type="time" :value="entry.end" @change="e => updateScheduleEntry(entry.day, 'end', e.target.value)" class="input flex-1 py-1.5 text-xs" />
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

                <!-- MQTT -->
                <section class="rounded-xl border border-gray-800 bg-gray-900 p-5">
                    <h3 class="mb-4 text-sm font-semibold tracking-wide text-gray-300 uppercase">MQTT</h3>
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
                <section class="rounded-xl border border-gray-800 bg-gray-900 p-5">
                    <h3 class="mb-4 text-sm font-semibold tracking-wide text-gray-300 uppercase">Social Media</h3>
                    <div v-for="(link, i) in socialLinks" :key="i" class="mb-2 flex gap-2">
                        <input v-model="link.platform" class="input flex-1" placeholder="Platform (e.g. facebook)" @input="socialLinks = [...socialLinks]" />
                        <input v-model="link.url" class="input flex-2 grow" placeholder="https://..." @input="socialLinks = [...socialLinks]" />
                        <button type="button" @click="removeSocialLink(i)" class="px-2 text-red-500 hover:text-red-400">✕</button>
                    </div>
                    <button type="button" @click="addSocialLink" class="mt-2 text-sm text-green-400 hover:text-green-300">+ Add link</button>
                </section>

                <div class="flex items-center gap-4">
                    <button type="submit" :disabled="saving" class="rounded-xl bg-green-600 px-6 py-2.5 font-semibold text-white hover:bg-green-500 disabled:opacity-50">
                        {{ saving ? 'Saving...' : 'Save Configuration' }}
                    </button>
                    <span v-if="saveMsg" class="text-sm text-green-400">{{ saveMsg }}</span>
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
                        <p class="font-mono text-white">{{ admin.diagnostics.activeQueueItemId ?? '—' }}</p>
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
