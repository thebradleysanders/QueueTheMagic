<script setup>
    import { computed, ref } from 'vue';

    import { useShowStore } from '@/stores/show';

    import NowPlayingCard from '@/components/NowPlayingCard.vue';
    import QueueList from '@/components/QueueList.vue';
    import SongRequestModal from '@/components/SongRequestModal.vue';

    const showModal = ref(false);
    const showStore = useShowStore();
    const queueDepth = computed(() => showStore.queue.filter(q => q.status === 'Pending').length);
</script>

<template>
    <div class="mx-auto max-w-lg px-4 pt-4 pb-6 md:max-w-2xl md:px-8">
        <NowPlayingCard class="mb-5" />

        <div class="mb-4 flex items-center justify-between">
            <div>
                <h2 class="text-main text-lg leading-tight font-bold">The Queue</h2>
                <p v-if="queueDepth > 0" class="text-hint text-xs">{{ queueDepth }} song{{ queueDepth !== 1 ? 's' : '' }} waiting</p>
            </div>
            <button v-if="showStore.config.isOpen" @click="showModal = true" class="rounded-xl px-4 py-2 text-sm font-semibold text-white transition active:scale-95" style="background: linear-gradient(135deg, #16a34a, #15803d)">+ Request</button>
        </div>

        <QueueList />
    </div>

    <SongRequestModal v-if="showModal" @close="showModal = false" />
</template>
