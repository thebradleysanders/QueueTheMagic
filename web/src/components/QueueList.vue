<script setup>
    import { computed, ref } from 'vue';

    import { useQueueDisplay } from '@/composables/useQueueDisplay';

    import { useShowStore } from '@/stores/show';

    import BumpModal from '@/components/BumpModal.vue';
    import MarqueeText from '@/components/MarqueeText.vue';

    const props = defineProps({
        limit: { type: Number, default: 0 }, // 0 = show everything
        hideEmpty: { type: Boolean, default: false }
    });

    const showStore = useShowStore();
    const { formatDuration, estimatedWaitSeconds, formatWait } = useQueueDisplay();

    const bumpItem = ref(null);
    const firstPendingIndex = computed(() => showStore.queue.findIndex(q => q.status === 'Pending'));
    const items = computed(() => (props.limit ? showStore.queue.slice(0, props.limit) : showStore.queue));
</script>

<template>
    <div>
        <div v-if="showStore.queue.length === 0 && !hideEmpty" class="border-base text-hint rounded-xl border border-dashed px-4 py-10 text-center text-sm">
            The queue is empty. Be the first to request a song.
        </div>

        <ul v-else-if="items.length" class="bg-card border-base divide-y divide-[var(--bd-base)] overflow-hidden rounded-xl border">
            <li v-for="(item, i) in items" :key="item.id" class="flex items-center gap-3 px-4 py-3" :class="item.status === 'Playing' ? 'row-live' : ''">
                <span class="tabular w-6 shrink-0 text-center text-sm font-semibold" :class="item.status === 'Playing' ? 'text-ac1' : 'text-hint'">
                    <font-awesome-icon v-if="item.status === 'Playing'" :icon="['fas', 'play']" class="text-xs" />
                    <template v-else>{{ i + 1 }}</template>
                </span>

                <div class="min-w-0 flex-1">
                    <MarqueeText :text="item.title" class="text-main text-sm font-medium" />
                    <p class="text-hint tabular mt-0.5 truncate text-xs">
                        <span v-if="item.artist || item.playlistName">{{ item.artist || item.playlistName }}</span>
                        <span v-if="item.durationSeconds"> · {{ formatDuration(item.durationSeconds) }}</span>
                        <span v-if="item.status === 'Pending' && estimatedWaitSeconds(i) > 0"> · starts in {{ formatWait(estimatedWaitSeconds(i)) }}</span>
                    </p>
                </div>

                <span v-if="item.status === 'Playing'" class="text-ac1 shrink-0 text-xs font-semibold">Playing</span>
                <span v-else-if="item.status === 'Pending' && i === firstPendingIndex" class="text-ac3 shrink-0 text-xs font-semibold">Up next</span>
                <button v-else-if="item.status === 'Pending'" @click="bumpItem = item" class="chip-boost shrink-0 rounded-lg px-3 py-1.5 text-xs font-medium transition active:scale-95">
                    Boost · ${{ showStore.config.bumpCost }}
                </button>
            </li>
        </ul>

        <BumpModal v-if="bumpItem" :item="bumpItem" @close="bumpItem = null" />
    </div>
</template>
