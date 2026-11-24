<script setup>
    import { computed } from 'vue';

    import { useShowStore } from '@/stores/show';

    import { version } from '../../package.json';

    const showStore = useShowStore();
    const config = computed(() => showStore.config);

    const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
    const todayName = DAYS[new Date().getDay()];

    const schedule = computed(() => {
        if (!config.value.showSchedule?.length) return [];
        return DAYS.map(day => {
            const entry = config.value.showSchedule.find(e => e.day === day);
            return entry ?? { day, start: '', end: '', enabled: false };
        });
    });

    function fmt12(time) {
        if (!time) return '';
        const labels = { dusk: 'Dusk', sunset: 'Sunset', sunrise: 'Sunrise', dawn: 'Dawn' };
        const lower = time.toLowerCase();
        if (labels[lower]) return labels[lower];
        const [h, m] = time.split(':').map(Number);
        if (isNaN(h)) return time;
        const ampm = h >= 12 ? 'PM' : 'AM';
        const h12 = h % 12 || 12;
        return m === 0 ? `${h12} ${ampm}` : `${h12}:${String(m).padStart(2, '0')} ${ampm}`;
    }

    function fmtDate(iso) {
        if (!iso) return null;
        const [y, m, d] = iso.split('-').map(Number);
        return new Date(y, m - 1, d).toLocaleDateString(undefined, { month: 'long', day: 'numeric' });
    }
    const seasonRange = computed(() => {
        const s = fmtDate(config.value.seasonStart);
        const e = fmtDate(config.value.seasonEnd);
        if (s && e) return `${s} – ${e}`;
        if (s) return `Starting ${s}`;
        if (e) return `Through ${e}`;
        return null;
    });

    const steps = computed(() => [
        { title: 'Pick a song', body: 'Browse the song list and choose your favorite.' },
        { title: 'Pay to request', body: `$${config.value.songRequestCost} adds your song to the queue. All of it goes toward the show.` },
        { title: 'Watch the lights', body: "Your song plays on the display, synced to the music. Rate it when it's done." },
        { title: 'Want it sooner?', body: `Boost a song for $${config.value.bumpCost} to move it up one spot.` }
    ]);
</script>

<template>
    <div class="mx-auto max-w-lg space-y-5 px-4 pt-5 pb-6 md:max-w-2xl md:px-8">
        <div>
            <h1 class="text-main text-xl font-semibold tracking-tight">Show info</h1>
            <p class="text-hint mt-0.5 text-sm">{{ config.siteName }}<span v-if="seasonRange"> · {{ seasonRange }}</span></p>
        </div>

        <div v-if="config.fmRadioStation" class="bg-card border-base flex items-center gap-4 rounded-xl border px-4 py-3.5">
            <font-awesome-icon :icon="['fas', 'radio']" class="text-hint text-lg" />
            <div>
                <p class="label">Listen on FM</p>
                <p class="tabular text-main text-xl leading-tight font-semibold">{{ config.fmRadioStation }}</p>
            </div>
        </div>

        <!-- Schedule -->
        <section>
            <h2 class="label mb-2 px-1">Schedule</h2>
            <div class="bg-card border-base overflow-hidden rounded-xl border">
                <div v-if="!schedule.length" class="text-hint px-4 py-6 text-center text-sm">Schedule not yet configured. Check back soon.</div>
                <ul v-else class="divide-y divide-[var(--bd-base)]">
                    <li v-for="entry in schedule" :key="entry.day" class="flex items-center gap-3 px-4 py-3" :class="entry.day === todayName && entry.enabled ? 'bg-ac1-soft' : ''">
                        <span class="w-28 shrink-0 text-sm" :class="entry.day === todayName ? 'text-main font-semibold' : 'text-sub'">
                            {{ entry.day }}
                            <span v-if="entry.day === todayName" class="text-ac1 ml-1.5 text-[10px] font-semibold tracking-wide uppercase">Today</span>
                        </span>
                        <span v-if="entry.enabled && entry.start && entry.end" class="tabular text-main text-sm">{{ fmt12(entry.start) }} – {{ fmt12(entry.end) }}</span>
                        <span v-else class="text-hint text-sm">Closed</span>
                    </li>
                </ul>
            </div>
        </section>

        <!-- How it works -->
        <section>
            <h2 class="label mb-2 px-1">How it works</h2>
            <ol class="bg-card border-base divide-y divide-[var(--bd-base)] overflow-hidden rounded-xl border">
                <li v-for="(step, i) in steps" :key="step.title" class="flex gap-3 px-4 py-3.5">
                    <span class="tabular text-hint w-5 shrink-0 text-sm font-semibold">{{ i + 1 }}</span>
                    <div>
                        <p class="text-main text-sm font-medium">{{ step.title }}</p>
                        <p class="text-hint mt-0.5 text-xs leading-relaxed">{{ step.body }}</p>
                    </div>
                </li>
            </ol>
        </section>

        <div class="border-base space-y-1 border-t pt-4 text-center">
            <p class="text-hint text-xs">Created by <span class="text-sub font-medium">Brad Sanders</span></p>
            <p class="text-hint text-xs">v{{ version }}</p>
        </div>
    </div>
</template>
