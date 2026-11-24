<script setup>
    import { ref } from 'vue';

    import { useCommunityStore } from '@/stores/community';
    import { useShowStore } from '@/stores/show';

    import DonateModal from '@/components/DonateModal.vue';
    import SupportersList from '@/components/SupportersList.vue';

    const showStore = useShowStore();
    const community = useCommunityStore();
    const showDonate = ref(false);
</script>

<template>
    <div class="mx-auto max-w-lg space-y-4 px-4 pt-5 pb-6 md:max-w-2xl md:px-8">
        <div>
            <h1 class="text-main text-xl font-semibold tracking-tight">Supporters</h1>
            <p class="text-hint mt-0.5 text-sm">
                Every request, boost and donation keeps the lights on.
                <template v-if="community.supporters > 0">Thank you to all {{ community.supporters }} of you!</template>
            </p>
        </div>

        <template v-if="showStore.config.showSupporters">
            <button @click="showDonate = true" class="btn-ac-2 w-full rounded-2xl px-4 py-3.5 text-sm">
                <font-awesome-icon :icon="['fas', 'heart']" class="mr-1.5" /> Add your name to the list
            </button>
            <SupportersList :limit="200" />
        </template>
        <p v-else class="text-hint py-10 text-center text-sm">The supporters list isn't available right now.</p>
    </div>

    <DonateModal v-if="showDonate" @close="showDonate = false" />
</template>
