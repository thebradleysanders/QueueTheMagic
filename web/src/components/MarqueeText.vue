<script setup>
    import { nextTick, onMounted, onUnmounted, ref, watch } from 'vue';

    const props = defineProps({ text: { type: String, default: '' } });

    const container = ref(null);
    const measurer = ref(null);
    const scrolling = ref(false);

    function measure() {
        if (!container.value || !measurer.value) return;
        scrolling.value = measurer.value.offsetWidth > container.value.offsetWidth;
    }

    let ro;
    onMounted(() => {
        nextTick(measure);
        ro = new ResizeObserver(measure);
        ro.observe(container.value);
    });
    onUnmounted(() => ro?.disconnect());
    watch(
        () => props.text,
        () => nextTick(measure)
    );
</script>

<template>
    <div ref="container" class="relative overflow-hidden whitespace-nowrap">
        <!-- Invisible measurer always present to detect overflow -->
        <span ref="measurer" class="pointer-events-none absolute top-0 left-0 whitespace-nowrap opacity-0" aria-hidden="true">{{ text }}</span>
        <!-- Scrolling: two copies so the loop is seamless -->
        <div v-if="scrolling" class="marquee-track inline-flex">
            <span class="marquee-item">{{ text }}</span>
            <span class="marquee-item" aria-hidden="true">{{ text }}</span>
        </div>
        <!-- Static: text fits, no animation -->
        <span v-else>{{ text }}</span>
    </div>
</template>

<style scoped>
    .marquee-item {
        padding-right: 3rem;
        flex-shrink: 0;
    }
    /* pause at start, then scroll, seamlessly loop (at -50% we're at the start of copy 2 = visually identical) */
    .marquee-track {
        animation: marquee 14s linear infinite;
    }
    @keyframes marquee {
        0%,
        12% {
            transform: translateX(0);
        }
        100% {
            transform: translateX(-50%);
        }
    }
</style>
