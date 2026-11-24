<script setup>
    import { useCommunityStore } from '@/stores/community';

    const community = useCommunityStore();

    function text(t) {
        switch (t.kind) {
            case 'request':
                return { icon: 'music', line: `${t.name ?? 'Someone'} requested ${t.title}` };
            case 'boost':
                return { icon: 'bolt', line: `${t.name ?? 'Someone'} boosted ${t.title}` };
            case 'donation':
                return { icon: 'gift', line: `${t.name ?? 'A neighbor'} donated $${Number(t.amount).toFixed(0)}` };
            default:
                return { icon: 'star', line: 'Something happened' };
        }
    }
</script>

<template>
    <div class="pointer-events-none fixed inset-x-0 top-16 z-40 flex flex-col items-center gap-2 px-4" aria-live="polite">
        <TransitionGroup name="toast">
            <div v-for="t in community.toasts" :key="t.id" class="toast flex max-w-sm items-center gap-2.5 rounded-full border py-2 pr-4 pl-2 text-sm shadow-lg">
                <span class="bg-ac1-soft text-ac1 flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs">
                    <font-awesome-icon :icon="['fas', text(t).icon]" />
                </span>
                <span class="text-main truncate">{{ text(t).line }}</span>
            </div>
        </TransitionGroup>
    </div>
</template>
