<script setup>
    import SupporterNameField from '@/components/SupporterNameField.vue';
    import { useSupporterName } from '@/composables/useSupporterName';
    import Celebrate from '@/components/Celebrate.vue';
    import axios from 'axios';

    import { nextTick, onMounted, onUnmounted, ref } from 'vue';

    import { usePayment } from '@/composables/usePayment';

    import { useSessionStore } from '@/stores/session';
    import { useShowStore } from '@/stores/show';

    const props = defineProps({ item: { type: Object, required: true } });
    const emit = defineEmits(['close', 'done']);

    const showStore = useShowStore();
    const sessionStore = useSessionStore();
    const { displayName } = useSupporterName();
    const { createIntent, mountPaymentElement, confirmPayment } = usePayment();

    const isDev = import.meta.env.DEV;
    const step = ref('loading'); // loading | payment | done
    const loading = ref(false);
    const errorMsg = ref(null);
    const paymentElementRef = ref(null);

    async function redeem(paymentIntentId) {
        await axios.put(`/api/queue/${props.item.id}/bump`, { paymentIntentId, sessionToken: sessionStore.sessionToken, displayName: displayName.value });
        await showStore.fetchQueue();
        step.value = 'done';
        emit('done');
    }

    function onKey(e) {
        if (e.key === 'Escape' && !loading.value) emit('close');
    }

    onMounted(async () => {
        window.addEventListener('keydown', onKey);
        try {
            if (isDev) {
                step.value = 'payment';
                return;
            }
            const clientSecret = await createIntent('bump', props.item.id);
            step.value = 'payment';
            await nextTick();
            await mountPaymentElement(clientSecret, paymentElementRef.value);
        } catch (e) {
            errorMsg.value = e.response?.data?.error || e.message || 'Could not start payment.';
            step.value = 'payment';
        }
    });
    onUnmounted(() => window.removeEventListener('keydown', onKey));

    async function submit() {
        loading.value = true;
        errorMsg.value = null;
        try {
            const paymentIntentId = isDev ? 'dev_test' : await confirmPayment(window.location.href);
            await redeem(paymentIntentId);
        } catch (e) {
            errorMsg.value = e.response?.data?.error || e.message || 'Boost failed.';
        } finally {
            loading.value = false;
        }
    }
</script>

<template>
    <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4" role="dialog" aria-modal="true" aria-label="Boost song" @click.self="emit('close')">
        <div class="bg-card border-base w-full max-w-sm overflow-hidden rounded-xl border">
            <div class="border-base flex items-center justify-between border-b px-5 py-4">
                <div class="min-w-0">
                    <h2 class="text-main text-base font-semibold">Boost this song</h2>
                    <p class="text-hint truncate text-xs">{{ item.title }}</p>
                </div>
                <button @click="emit('close')" aria-label="Close" class="text-hint hover:text-main flex h-8 w-8 items-center justify-center text-xl">
                    <font-awesome-icon :icon="['fas', 'xmark']" />
                </button>
            </div>

            <div v-if="step === 'loading'" class="text-hint p-8 text-center text-sm">Preparing secure payment…</div>

            <div v-if="step === 'payment'" class="p-5">
                <p class="text-sub mb-3 text-sm">Move this song up one spot in the queue.</p>
                <div v-if="errorMsg" class="mb-3 rounded-xl bg-red-500/20 p-3 text-sm text-red-500">{{ errorMsg }}</div>
                <SupporterNameField class="mb-4" />
                <div ref="paymentElementRef"></div>
                <button @click="submit" :disabled="loading" class="btn-ac mt-4 w-full rounded-xl py-4 text-base font-semibold transition active:scale-95 disabled:opacity-40">
                    {{ loading ? 'Processing…' : isDev ? 'Boost (dev)' : `Boost · $${showStore.config.bumpCost}` }}
                </button>
            </div>

            <div v-if="step === 'done'" class="p-8 text-center">
                <Celebrate :count="22" :spread="120"><font-awesome-icon :icon="['fas', 'bolt']" class="text-ac3 mb-3 text-3xl" /></Celebrate>
                <p class="text-main text-lg font-semibold">Boosted!</p>
                <p class="text-hint mt-1 text-sm">Your song moved up the queue.</p>
                <button @click="emit('close')" class="btn-ac mt-5 rounded-xl px-8 py-3 text-sm font-semibold transition active:scale-95">Done</button>
            </div>
        </div>
    </div>
</template>
