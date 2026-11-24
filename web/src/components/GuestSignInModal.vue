<script setup>
    import { onMounted, onUnmounted, ref } from 'vue';

    import { useGuestStore } from '@/stores/guest';

    const emit = defineEmits(['close']);
    const guest = useGuestStore();

    const code = ref('');
    const error = ref(null);
    const loading = ref(false);
    const input = ref(null);

    function onKey(e) {
        if (e.key === 'Escape') emit('close');
    }
    onMounted(() => {
        window.addEventListener('keydown', onKey);
        input.value?.focus();
    });
    onUnmounted(() => window.removeEventListener('keydown', onKey));

    async function submit() {
        if (code.value.trim().length < 4 || loading.value) return;
        loading.value = true;
        error.value = null;
        try {
            await guest.signIn(code.value);
            emit('close');
        } catch (e) {
            error.value = e.response?.data?.error || (e.response?.status === 429 ? 'Too many tries. Wait a minute and try again.' : 'Could not sign in. Please try again.');
        } finally {
            loading.value = false;
        }
    }
</script>

<template>
    <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4" role="dialog" aria-modal="true" aria-label="Friends and family sign in" @click.self="emit('close')">
        <form @submit.prevent="submit" class="bg-card border-base w-full max-w-sm overflow-hidden rounded-2xl border">
            <div class="border-base flex items-center justify-between border-b px-5 py-4">
                <h2 class="text-main text-lg font-semibold">Friends &amp; family</h2>
                <button type="button" @click="emit('close')" aria-label="Close" class="text-hint hover:text-main text-lg"><font-awesome-icon :icon="['fas', 'xmark']" /></button>
            </div>
            <div class="p-5">
                <p class="text-sub mb-4 text-sm">Enter the code you were given to pick songs for free, any time.</p>
                <div v-if="error" class="mb-3 rounded-lg bg-red-500/20 p-3 text-sm text-red-400">{{ error }}</div>
                <input
                    ref="input"
                    v-model="code"
                    type="text"
                    inputmode="text"
                    autocomplete="off"
                    autocapitalize="characters"
                    spellcheck="false"
                    maxlength="12"
                    placeholder="ABC123"
                    class="bg-lift border-base text-main focus-ac1 tabular w-full rounded-lg border px-4 py-3 text-center text-xl font-semibold tracking-[0.3em] uppercase"
                />
                <button type="submit" :disabled="code.trim().length < 4 || loading" class="btn-ac mt-4 w-full rounded-xl py-3 text-sm">
                    {{ loading ? 'Checking…' : 'Sign in' }}
                </button>
            </div>
        </form>
    </div>
</template>
