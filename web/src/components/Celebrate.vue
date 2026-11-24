<script setup>
    import { onMounted, ref } from 'vue';

    import { burst } from '@/composables/useBurst';

    /** Wraps a "thank you" icon and fires one burst of lights when it appears. */
    const props = defineProps({ count: { type: Number, default: 28 }, spread: { type: Number, default: 140 } });
    const el = ref(null);
    onMounted(() => burst(el.value, { count: props.count, spread: props.spread }));
</script>

<template>
    <span ref="el" class="inline-block"><slot /></span>
</template>
