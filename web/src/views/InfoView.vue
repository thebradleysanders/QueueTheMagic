<script setup>
    import { computed } from 'vue';

    import { useShowStore } from '@/stores/show';

    import { version } from '../../package.json';

    const showStore = useShowStore();
    const config = computed(() => showStore.config);
    const isDark = computed(() => config.value.theme !== 'light');

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

    const radioStyle = computed(() =>
        isDark.value
            ? 'bg-gradient-to-br from-slate-900 via-indigo-950 to-slate-900 border border-violet-400/35 shadow-[0_0_32px_rgba(139,92,246,0.15)]'
            : 'bg-gradient-to-br from-violet-50 via-violet-100 to-violet-50 border border-violet-400/30 shadow-[0_2px_16px_rgba(139,92,246,0.1)]'
    );
</script>

<template>
    <div class="mx-auto max-w-lg px-4 pt-4 pb-6 md:max-w-2xl md:px-8">
        <div class="mb-6">
            <h1 class="text-main text-2xl font-black">Show Info</h1>
            <p class="text-hint mt-1 text-sm">{{ config.siteName }}</p>
        </div>

        <!-- FM Radio -->
        <div v-if="config.fmRadioStation" class="relative mb-5 overflow-hidden rounded-2xl" :class="radioStyle">
            <div class="pointer-events-none absolute inset-0" :style="isDark ? 'background: radial-gradient(ellipse at 50% 0%, rgba(139,92,246,0.2) 0%, transparent 70%)' : 'background: radial-gradient(ellipse at 50% 0%, rgba(139,92,246,0.1) 0%, transparent 70%)'" />
            <div class="relative flex items-center gap-4 px-5 py-4">
                <div class="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl text-2xl" :style="isDark ? 'background: rgba(139,92,246,0.2); border: 1px solid rgba(139,92,246,0.3)' : 'background: rgba(139,92,246,0.15); border: 1px solid rgba(139,92,246,0.25)'">
                    📻
                </div>
                <div>
                    <p class="mb-0.5 text-xs font-semibold tracking-widest uppercase" :style="isDark ? 'color: rgba(167,139,250,0.8)' : 'color: rgba(109,40,217,0.8)'">Tune your radio to</p>
                    <p class="text-2xl leading-none font-black" :style="isDark ? 'color: #ffffff' : 'color: #3b0764'">
                        {{ config.fmRadioStation }}
                    </p>
                    <p class="mt-1 text-xs" :style="isDark ? 'color: rgba(167,139,250,0.6)' : 'color: rgba(109,40,217,0.6)'">to hear the show live</p>
                </div>
            </div>
        </div>

        <!-- Show Schedule -->
        <div class="bg-card border-base mb-5 overflow-hidden rounded-2xl border">
            <div class="border-base flex items-center gap-2 border-b px-5 py-4">
                <font-awesome-icon :icon="['fas', 'calendar-days']" class="text-lg" />
                <h2 class="text-main font-bold">Show Schedule</h2>
            </div>

            <div v-if="!schedule.length" class="text-hint px-5 py-6 text-center text-sm">Schedule not yet configured, check back soon!</div>

            <div v-else>
                <div v-for="entry in schedule" :key="entry.day" class="border-base flex items-center gap-4 border-b px-5 py-3.5 transition last:border-0" :class="entry.day === todayName && entry.enabled ? (isDark ? 'bg-green-500/5' : 'bg-green-50') : ''">
                    <div class="h-8 w-1.5 shrink-0 rounded-full" :class="entry.day === todayName ? (entry.enabled ? 'bg-green-500' : 'bg-ghost') : 'bg-transparent'" />

                    <div class="w-24 shrink-0">
                        <span class="text-sm font-semibold" :class="entry.day === todayName ? 'text-main' : 'text-sub'">
                            {{ entry.day }}
                        </span>
                        <span v-if="entry.day === todayName" class="ml-2 text-[10px] font-bold tracking-wide text-green-500 uppercase">Today</span>
                    </div>

                    <div class="flex-1">
                        <span v-if="entry.enabled && entry.start && entry.end" class="text-sm font-semibold" :class="entry.day === todayName ? 'text-green-600' : 'text-main'">{{ fmt12(entry.start) }} - {{ fmt12(entry.end) }}</span>
                        <span v-else class="text-hint text-sm italic">Closed</span>
                    </div>
                </div>
            </div>
        </div>

        <!-- How it works -->
        <div class="bg-card border-base mb-5 overflow-hidden rounded-2xl border">
            <div class="border-base flex items-center gap-2 border-b px-5 py-4">
                <font-awesome-icon :icon="['fas', 'lightbulb']" class="text-lg" />
                <h2 class="text-main font-bold">How It Works</h2>
            </div>
            <div class="space-y-4 px-5 py-4">
                <div class="flex gap-3">
                    <div class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-green-500/30 bg-green-500/15 text-sm font-bold text-green-600">1</div>
                    <div>
                        <p class="text-main text-sm font-semibold">Pick a song</p>
                        <p class="text-hint mt-0.5 text-xs">Browse the playlist and pick your favorite holiday tune.</p>
                    </div>
                </div>
                <div class="flex gap-3">
                    <div class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-green-500/30 bg-green-500/15 text-sm font-bold text-green-600">2</div>
                    <div>
                        <p class="text-main text-sm font-semibold">Make a small donation</p>
                        <p class="text-hint mt-0.5 text-xs">Just ${{ config.songRequestCost }} adds your song to the queue. 100% goes toward the show.</p>
                    </div>
                </div>
                <div class="flex gap-3">
                    <div class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-green-500/30 bg-green-500/15 text-sm font-bold text-green-600">3</div>
                    <div>
                        <p class="text-main text-sm font-semibold">Watch the lights dance</p>
                        <p class="text-hint mt-0.5 text-xs">Your song plays on the display synced to the music. Rate it when it's done!</p>
                    </div>
                </div>
                <div class="flex gap-3">
                    <div class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-yellow-500/30 bg-yellow-500/15 text-sm font-bold text-yellow-600">⚡</div>
                    <div>
                        <p class="text-main text-sm font-semibold">Want to go sooner?</p>
                        <p class="text-hint mt-0.5 text-xs">Use the Boost button for ${{ config.bumpCost }} to move your song up one spot in the queue.</p>
                    </div>
                </div>
            </div>
        </div>

        <!-- Footer -->
        <div class="border-base mt-6 space-y-1 border-t pt-4 text-center">
            <p class="text-hint text-xs">
                Created By:
                <a href="https://github.com/thebradleysanders/" target="_blank" rel="noopener" class="text-sub hover:text-main font-semibold transition">Brad Sanders</a>
                &nbsp;·&nbsp;
                <a href="https://github.com/thebradleysanders/QueueTheMagic" target="_blank" rel="noopener" class="hover:text-main inline-flex items-center gap-1 transition">
                    <i class="fa-brands fa-github text-sm"></i>
                    QueueTheMagic
                </a>
            </p>
            <p class="text-hint text-xs">v{{ version }}</p>
        </div>
    </div>
</template>
