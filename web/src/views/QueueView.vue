<script setup>
import { ref, computed } from 'vue'
import NowPlayingCard from '@/components/NowPlayingCard.vue'
import QueueList from '@/components/QueueList.vue'
import SongRequestModal from '@/components/SongRequestModal.vue'
import { useShowStore } from '@/stores/show'

const showModal = ref(false)
const showStore = useShowStore()
const queueDepth = computed(() => showStore.queue.filter(q => q.status === 'Pending').length)
</script>

<template>
  <div class="max-w-lg md:max-w-2xl mx-auto px-4 md:px-8 pt-4 pb-6">

    <NowPlayingCard class="mb-5" />

    <div class="flex items-center justify-between mb-4">
      <div>
        <h2 class="font-bold text-main text-lg leading-tight">The Queue</h2>
        <p v-if="queueDepth > 0" class="text-xs text-hint">{{ queueDepth }} song{{ queueDepth !== 1 ? 's' : '' }} waiting</p>
      </div>
      <button
        v-if="showStore.config.isOpen"
        @click="showModal = true"
        class="text-sm font-semibold px-4 py-2 rounded-xl text-white transition active:scale-95"
        style="background: linear-gradient(135deg, #16a34a, #15803d)"
      >
        + Request
      </button>
    </div>

    <QueueList />

  </div>

  <SongRequestModal v-if="showModal" @close="showModal = false" />
</template>
