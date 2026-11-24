<script setup>
    import { ref } from 'vue';

    import { burst } from '@/composables/useBurst';

    import { useCommunityStore } from '@/stores/community';

    const community = useCommunityStore();
    const btn = ref(null);
    const pressed = ref(false);
    const coolingDown = ref(false);

    async function cheer() {
        if (coolingDown.value) return;
        burst(btn.value);
        pressed.value = true;
        setTimeout(() => (pressed.value = false), 180);
        navigator.vibrate?.(15);

        coolingDown.value = true;
        await community.cheer();
        setTimeout(() => (coolingDown.value = false), 2000);
    }
</script>

<template>
    <div class="bg-card border-base flex items-center gap-4 rounded-2xl border p-4">
        <button
            ref="btn"
            @click="cheer"
            class="cheer-btn relative flex h-16 w-16 shrink-0 items-center justify-center rounded-full text-2xl"
            :class="{ pressed, cooling: coolingDown }"
            aria-label="Send a cheer to the show"
        >
            <font-awesome-icon :icon="['fas', 'hands-clapping']" />
        </button>
        <div class="min-w-0">
            <p class="text-main font-semibold">Cheer for the show</p>
            <p class="text-hint text-xs leading-relaxed">Tap to send some cheer. Everyone watching sees the lights flash.</p>
            <p class="text-ac3 tabular mt-1 text-xs font-semibold">{{ community.cheersToday.toLocaleString() }} cheers tonight</p>
        </div>
    </div>
</template>
