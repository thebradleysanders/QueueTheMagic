<script setup>
    import { computed, ref } from 'vue';

    import { useCommunityStore } from '@/stores/community';
    import { useShowStore } from '@/stores/show';

    import CheerButton from '@/components/CheerButton.vue';
    import DonateModal from '@/components/DonateModal.vue';
    import DonationGoal from '@/components/DonationGoal.vue';
    import ListenCard from '@/components/ListenCard.vue';
    import NowPlayingCard from '@/components/NowPlayingCard.vue';
    import QueueList from '@/components/QueueList.vue';
    import SongRequestModal from '@/components/SongRequestModal.vue';
    import SupportersList from '@/components/SupportersList.vue';

    const showStore = useShowStore();
    const community = useCommunityStore();
    const showModal = ref(false);
    const showDonate = ref(false);
    const isOpen = computed(() => showStore.config.isOpen);
    const waiting = computed(() => showStore.queue.filter(q => q.status === 'Pending').length);
</script>

<template>
    <div class="mx-auto max-w-lg space-y-4 px-4 pt-4 pb-6 md:max-w-2xl md:px-8">
        <NowPlayingCard />

        <!-- Audio: listen on the phone and/or FM -->
        <ListenCard />

        <!-- Request: the main event -->
        <button v-if="isOpen" @click="showModal = true" class="cta-request flex w-full items-center justify-between gap-3 rounded-2xl px-5 py-4 text-left">
            <span>
                <span class="block text-lg leading-tight font-bold">Pick the next song</span>
                <span class="block text-sm opacity-75">{{ waiting === 0 ? 'Nothing in the queue. Yours plays next.' : `${waiting} in the queue. Jump in!` }}</span>
            </span>
            <span class="shrink-0 rounded-full bg-black/15 px-3 py-1 text-sm font-bold">${{ showStore.config.songRequestCost }}</span>
        </button>
        <div v-else class="bg-card border-base text-sub rounded-2xl border px-4 py-3 text-center text-sm">
            Song requests are closed right now. See the Info tab for show hours.
        </div>

        <CheerButton />

        <DonationGoal @donate="showDonate = true" />

        <!-- Up next -->
        <section v-if="showStore.queue.length > 0">
            <div class="mb-2 flex items-baseline justify-between px-1">
                <h2 class="label">Up next</h2>
                <RouterLink v-if="showStore.queue.length > 3" to="/queue" class="text-hint hover:text-main text-xs transition">View all {{ showStore.queue.length }}</RouterLink>
            </div>
            <QueueList :limit="3" hide-empty />
        </section>

        <!-- Supporters -->
        <section v-if="showStore.config.showSupporters">
            <div class="mb-2 flex items-baseline justify-between px-1">
                <h2 class="label">Recent supporters</h2>
                <RouterLink to="/supporters" class="text-hint hover:text-main text-xs transition">See all</RouterLink>
            </div>
            <SupportersList :limit="5" />
        </section>

        <!-- Donate without a goal configured -->
        <button v-if="community.goal <= 0" @click="showDonate = true" class="btn-quiet w-full rounded-2xl px-4 py-3.5 text-sm">
            <font-awesome-icon :icon="['fas', 'gift']" class="text-ac2 mr-1.5" /> Enjoying the show? Leave a tip
        </button>
    </div>

    <SongRequestModal v-if="showModal" @close="showModal = false" />
    <DonateModal v-if="showDonate" @close="showDonate = false" />
</template>
