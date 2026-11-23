<script setup>
    import { computed } from 'vue';

    import { useSeason } from '@/composables/useSeason';

    defineProps({ variant: { type: String, default: 'particles' } }); // 'lights' | 'particles'
    const { season } = useSeason();

    // Light string: bulbs alternate between the season's three accent colours.
    const bulbs = Array.from({ length: 26 }, (_, i) => ({
        left: `${(i + 0.5) * (100 / 26)}%`,
        top: `${Math.sin((i / 25) * Math.PI) * 4}px`,
        color: `rgb(var(--ac${(i % 3) + 1}))`,
        delay: `${((i * 0.37) % 2.4).toFixed(2)}s`
    }));

    // Particles are positioned once so they don't jump on re-render.
    const COUNT = 14;
    const seeds = Array.from({ length: COUNT }, (_, i) => ({
        left: `${Math.round((i / COUNT) * 100 + Math.random() * 6)}%`,
        size: `${(0.7 + Math.random() * 0.9).toFixed(2)}rem`,
        duration: `${(14 + Math.random() * 14).toFixed(1)}s`,
        delay: `-${(Math.random() * 26).toFixed(1)}s`,
        drift: `${Math.round(-60 + Math.random() * 120)}px`
    }));

    const particles = computed(() => seeds.map((p, i) => ({ ...p, char: season.value.particles[i % season.value.particles.length] })));
</script>

<template>
    <div aria-hidden="true">
        <div v-if="variant === 'lights'" class="light-string">
            <span v-for="(b, i) in bulbs" :key="i" class="bulb" :style="{ left: b.left, marginTop: b.top, '--c': b.color, animationDelay: b.delay }" />
        </div>
        <template v-if="variant === 'particles'"><span v-for="(p, i) in particles" :key="`${season.id}-${i}`" class="season-particle" :style="{ left: p.left, fontSize: p.size, animationDuration: p.duration, animationDelay: p.delay, '--drift': p.drift }">{{ p.char }}</span></template>
    </div>
</template>
