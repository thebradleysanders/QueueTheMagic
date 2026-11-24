<script setup>
    import { computed } from 'vue';

    import { useCommunityStore } from '@/stores/community';

    const emit = defineEmits(['donate']);
    const community = useCommunityStore();

    const pct = computed(() => (community.goal > 0 ? Math.min(100, (community.raised / community.goal) * 100) : 0));
    const money = v => `$${Math.round(v).toLocaleString()}`;

    // Bulbs along the bar light up as the goal fills.
    const BULBS = 20;
    const lit = computed(() => Math.round((pct.value / 100) * BULBS));
</script>

<template>
    <div v-if="community.goal > 0" class="goal-card rounded-2xl border p-4">
        <div class="flex items-start justify-between gap-3">
            <div class="min-w-0">
                <p class="label !text-[rgb(var(--ac3))]">Season goal</p>
                <p class="text-main mt-0.5 font-semibold leading-snug">{{ community.goalLabel || 'Help keep the lights on' }}</p>
            </div>
            <p class="tabular shrink-0 text-right">
                <span class="text-main text-lg font-bold">{{ money(community.raised) }}</span>
                <span class="text-hint block text-xs">of {{ money(community.goal) }}</span>
            </p>
        </div>

        <div class="goal-bulbs mt-4" :aria-label="`${Math.round(pct)}% of the goal raised`" role="img">
            <span v-for="i in BULBS" :key="i" class="goal-bulb" :class="{ on: i <= lit }" :style="{ '--c': `rgb(var(--ac${(i % 3) + 1}))`, '--d': `${(i * 0.17) % 2}s` }" />
        </div>

        <div class="mt-3 flex items-center justify-between gap-3">
            <p class="text-hint text-xs">
                <template v-if="pct >= 100">Goal reached. Thank you, neighbors!</template>
                <template v-else-if="community.supporters > 0">{{ community.supporters }} {{ community.supporters === 1 ? 'neighbor has' : 'neighbors have' }} chipped in</template>
                <template v-else>Be the first to chip in this season</template>
            </p>
            <button @click="emit('donate')" class="btn-ac-2 shrink-0 rounded-lg px-4 py-2 text-sm transition active:scale-95">Chip in</button>
        </div>
    </div>
</template>
