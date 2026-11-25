<script setup>
    // Section switcher used inside the admin tabs: a sticky side menu on desktop,
    // a horizontally scrolling row of pills on phones.
    defineProps({
        items: { type: Array, required: true }, // [{ key, label, hint? }]
        modelValue: { type: String, required: true }
    });
    defineEmits(['update:modelValue']);
</script>

<template>
    <nav class="-mx-4 mb-5 flex gap-2 overflow-x-auto px-4 md:sticky md:top-4 md:mx-0 md:mb-0 md:flex-col md:gap-1 md:self-start md:overflow-visible md:px-0" aria-label="Sections">
        <button
            v-for="item in items"
            :key="item.key"
            type="button"
            @click="$emit('update:modelValue', item.key)"
            class="shrink-0 rounded-lg border px-3 py-2 text-left text-sm whitespace-nowrap transition md:whitespace-normal"
            :class="modelValue === item.key ? 'border-gray-600 bg-gray-800 text-white' : 'border-gray-800 text-gray-400 hover:border-gray-700 hover:text-white md:border-transparent'"
            :aria-current="modelValue === item.key ? 'true' : undefined"
        >
            <span class="block font-medium">{{ item.label }}</span>
            <span v-if="item.hint" class="mt-0.5 hidden text-xs font-normal text-gray-500 md:block">{{ item.hint }}</span>
        </button>
    </nav>
</template>
