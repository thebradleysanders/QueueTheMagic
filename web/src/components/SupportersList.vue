<script setup>
    import axios from 'axios';

    import { onMounted, ref, watch } from 'vue';

    import { useCommunityStore } from '@/stores/community';
    import { useShowStore } from '@/stores/show';

    const props = defineProps({ limit: { type: Number, default: 50 } });

    const showStore = useShowStore();
    const community = useCommunityStore();
    const rows = ref([]);
    const loaded = ref(false);

    async function load() {
        if (!showStore.config.showSupporters) return;
        try {
            const { data } = await axios.get('/api/show/supporters', { params: { limit: props.limit } });
            rows.value = data;
        } catch {
            rows.value = [];
        } finally {
            loaded.value = true;
        }
    }

    onMounted(load);
    // New payment anywhere (or an admin hiding a name) → refresh
    watch(() => community.pulse.n, () => community.pulse.kind !== 'cheer' && load());
    watch(() => community.supportersVersion, load);
    watch(() => showStore.config.showSupporters, load);

    const verb = { request: 'requested a song', boost: 'boosted a song', donation: 'donated' };

    function ago(iso) {
        const secs = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);
        if (secs < 60) return 'just now';
        if (secs < 3600) return `${Math.floor(secs / 60)}m ago`;
        if (secs < 86400) return `${Math.floor(secs / 3600)}h ago`;
        const days = Math.floor(secs / 86400);
        return days === 1 ? 'yesterday' : new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
    }
    const initial = name => (name ? name.trim()[0].toUpperCase() : '');
</script>

<template>
    <div v-if="showStore.config.showSupporters">
        <div v-if="loaded && !rows.length" class="border-base text-hint rounded-2xl border border-dashed px-4 py-8 text-center text-sm">No supporters yet this season. You could be the first!</div>

        <ul v-else-if="rows.length" class="bg-card border-base divide-y divide-[var(--bd-base)] overflow-hidden rounded-2xl border">
            <li v-for="(r, i) in rows" :key="i" class="flex items-center gap-3 px-4 py-2.5">
                <span class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-xs font-bold" :class="r.name ? 'text-black' : 'bg-lift text-hint'" :style="r.name ? `background: rgb(var(--ac${(i % 3) + 1}))` : ''">
                    <template v-if="r.name">{{ initial(r.name) }}</template>
                    <font-awesome-icon v-else :icon="['fas', 'gift']" />
                </span>
                <div class="min-w-0 flex-1">
                    <p class="truncate text-sm" :class="r.name ? 'text-main font-medium' : 'text-sub'">{{ r.name || 'Anonymous' }}</p>
                    <p class="text-hint text-xs">{{ verb[r.kind] ?? 'chipped in' }} · {{ ago(r.at) }}</p>
                </div>
                <span class="tabular text-main shrink-0 text-sm font-semibold">${{ Number(r.amount) % 1 === 0 ? Number(r.amount) : Number(r.amount).toFixed(2) }}</span>
            </li>
        </ul>
    </div>
</template>
