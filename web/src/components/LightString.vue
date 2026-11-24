<script setup>
    import { ref, watch } from 'vue';

    import { useCommunityStore } from '@/stores/community';

    /**
     * A string of bulbs under the header. It's a light show, so the page should glow a little,
     * and it reacts: a cheer makes every bulb flash, a request or donation runs a chase.
     */
    const community = useCommunityStore();
    const COUNT = 24;
    const bulbs = Array.from({ length: COUNT }, (_, i) => ({
        left: `${((i + 0.5) / COUNT) * 100}%`,
        sag: `${Math.sin(((i + 0.5) / COUNT) * Math.PI) * 5}px`,
        color: `rgb(var(--ac${(i % 3) + 1}))`,
        delay: `${((i * 0.41) % 3).toFixed(2)}s`
    }));

    const mode = ref(''); // '', 'flash', 'chase'
    let timer;
    watch(
        () => community.pulse.n,
        () => {
            mode.value = '';
            requestAnimationFrame(() => (mode.value = community.pulse.kind === 'cheer' ? 'flash' : 'chase'));
            clearTimeout(timer);
            timer = setTimeout(() => (mode.value = ''), 1600);
        }
    );
</script>

<template>
    <div class="light-string" :class="mode" aria-hidden="true">
        <span v-for="(b, i) in bulbs" :key="i" class="bulb" :style="{ left: b.left, marginTop: b.sag, '--c': b.color, '--d': b.delay, '--i': i }" />
    </div>
</template>
