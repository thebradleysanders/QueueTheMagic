<script setup>
    import { useSupporterName } from '@/composables/useSupporterName';

    import { useShowStore } from '@/stores/show';

    const showStore = useShowStore();
    const { name, showName } = useSupporterName();
</script>

<template>
    <div v-if="showStore.config.showSupporters" class="bg-lift border-base rounded-xl border p-3">
        <label class="flex cursor-pointer items-center justify-between gap-3">
            <span class="min-w-0">
                <span class="text-main block text-sm font-medium">Show my name on the supporters list</span>
                <span class="text-hint block text-xs">{{ showName ? 'Everyone will see this name.' : "You'll appear as Anonymous." }}</span>
            </span>
            <span class="switch shrink-0" :class="{ on: showName }">
                <input type="checkbox" v-model="showName" class="sr-only" />
                <span class="knob" />
            </span>
        </label>
        <input
            v-if="showName"
            v-model="name"
            type="text"
            maxlength="30"
            autocomplete="nickname"
            placeholder="e.g. The Smith Family"
            class="bg-card border-base text-main focus-ac1 mt-3 w-full rounded-lg border px-3 py-2 text-sm"
        />
    </div>
</template>

<style scoped>
    .switch {
        position: relative;
        width: 40px;
        height: 24px;
        border-radius: 9999px;
        background: var(--bd-strong);
        transition: background 0.2s;
    }
    .switch.on {
        background: rgb(var(--ac1));
    }
    .knob {
        position: absolute;
        top: 3px;
        left: 3px;
        width: 18px;
        height: 18px;
        border-radius: 9999px;
        background: #fff;
        transition: transform 0.2s;
    }
    .switch.on .knob {
        transform: translateX(16px);
    }
</style>
